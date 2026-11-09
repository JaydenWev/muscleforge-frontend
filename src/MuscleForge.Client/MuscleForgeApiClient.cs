using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MuscleForge.Client;

public sealed class ApiError
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Code { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}

public sealed class ApiException(HttpStatusCode statusCode, ApiError? problem, RetryConditionHeaderValue? retryAfter)
    : HttpRequestException($"The API request failed with status {(int)statusCode}.", null, statusCode)
{
    public ApiError? Problem { get; } = problem;
    public RetryConditionHeaderValue? RetryAfter { get; } = retryAfter;
}

public sealed class MuscleForgeApiClient(HttpClient httpClient)
{
    private async Task<T> Invoke<T>(Uri baseUrl, string? token, Func<RawApiClient, Task<T>> operation)
    {
        using var transport = new HttpClient(new SafeTransport(httpClient));
        var client = new RawApiClient(baseUrl.AbsoluteUri, transport) { BearerToken = token };
        try { return await operation(client).ConfigureAwait(false); }
        catch (RawApiException error) when (error.StatusCode == 200) { throw new JsonException("The API returned an invalid response."); }
        catch (RawApiException error) { throw new ApiException((HttpStatusCode)error.StatusCode, null, null); }
    }

    public Task<AccessTokenResponse> LoginAsync(Uri baseUrl, LoginRequest body, CancellationToken cancellationToken = default) =>
        Invoke(baseUrl, null, c => c.LoginAsync(body, cancellationToken));

    public Task<AccessTokenResponse> RefreshAsync(Uri baseUrl, RefreshRequest body, CancellationToken cancellationToken = default) =>
        Invoke(baseUrl, null, c => c.RefreshAsync(body, cancellationToken));

    public Task<CurrentUserResponse> GetCurrentUserAsync(Uri baseUrl, string bearerToken, CancellationToken cancellationToken = default) =>
        Invoke(baseUrl, bearerToken, c => c.GetCurrentUserAsync(cancellationToken));

    public Task<ApiStatusResponse> GetApiStatusAsync(Uri baseUrl, CancellationToken cancellationToken = default) =>
        Invoke(baseUrl, null, c => c.GetApiStatusAsync(cancellationToken));

    private sealed class SafeTransport(HttpClient inner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri) { Content = request.Content, Version = request.Version, VersionPolicy = request.VersionPolicy };
            foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await inner.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK) return response;
            using (response)
            {
                ApiError? problem = null;
                if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
                {
                    try
                    {
                        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                        using var buffer = new MemoryStream();
                        var chunk = new byte[4096];
                        int count;
                        while ((count = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            if (buffer.Length + count > 65536) break;
                            buffer.Write(chunk, 0, count);
                        }
                        if (count == 0)
                        {
                            buffer.Position = 0;
                            problem = await JsonSerializer.DeserializeAsync<ApiError>(buffer, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (JsonException) { }
                }
                throw new ApiException(response.StatusCode, problem, response.Headers.RetryAfter);
            }
        }
    }
}
