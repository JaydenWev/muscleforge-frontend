using System.Net;
using System.Text;
using System.Text.Json;
using MuscleForge.Client;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public partial class GeneratedApiClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class StreamingContent(Stream stream) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
            throw new InvalidOperationException("The response must not be buffered.");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(stream);
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult(stream);
    }

    private sealed class OversizedStream : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Span.Fill((byte)'x');
            BytesRead += buffer.Length;
            return ValueTask.FromResult(buffer.Length);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, mediaType)
    };

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(429)]
    public async Task AuthProblemsAreTypedAndMessagesAreSafe(int status)
    {
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var response = Json((HttpStatusCode)status, "{\"code\":\"invalid\",\"traceId\":\"trace\",\"errors\":{\"email\":[\"bad\"]}}", "application/problem+json");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
            return Task.FromResult(response);
        }));
        var client = new MuscleForgeApiClient(http);
        var error = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync(new Uri("https://example.test/"), new LoginRequest { Email = "example", Password = "secret-password" }, TestContext.Current.CancellationToken));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal("invalid", error.Problem!.Code);
        Assert.Equal("trace", error.Problem.TraceId);
        Assert.Equal("bad", error.Problem.Errors!["email"][0]);
        Assert.Equal(TimeSpan.FromSeconds(12), error.RetryAfter!.Delta);
        Assert.DoesNotContain("secret-password", error.Message);
    }

    [Fact]
    public async Task AuthSuccessSupportsStringExpiryAndBearerMe()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login")
            ? Json(HttpStatusCode.OK, "{\"accessToken\":\"access\",\"refreshToken\":\"refresh\",\"expiresIn\":\"900\"}")
            : Json(HttpStatusCode.OK, $"{{\"id\":\"{id}\",\"email\":\"test@example.test\"}}"))));
        var client = new MuscleForgeApiClient(http);
        var login = await client.LoginAsync(new Uri("https://example.test/"), new LoginRequest { Email = "test", Password = "test" }, TestContext.Current.CancellationToken);
        Assert.Equal(900, login.ExpiresIn);
        var me = await client.GetCurrentUserAsync(new Uri("https://example.test/"), login.AccessToken, TestContext.Current.CancellationToken);
        Assert.Equal(id, me.Id);
    }

    [Fact]
    public async Task RefreshIsAnonymousAndNonJsonFailureIsSafe()
    {
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.Null(request.Headers.Authorization);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("test-refresh", body.RootElement.GetProperty("refreshToken").GetString());
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret untrusted body") };
        }));
        var error = await Assert.ThrowsAsync<ApiException>(() => new MuscleForgeApiClient(http).RefreshAsync(
            new Uri("https://example.test/"), new RefreshRequest { RefreshToken = "test-refresh" }, TestContext.Current.CancellationToken));
        Assert.Null(error.Problem);
        Assert.DoesNotContain("secret", error.Message);
        Assert.DoesNotContain("test-refresh", error.Message);
    }

    [Fact]
    public async Task OversizedProblemIsStreamedAndBounded()
    {
        var stream = new OversizedStream();
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var content = new StreamingContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content });
        }));
        var error = await Assert.ThrowsAsync<ApiException>(() => new MuscleForgeApiClient(http).GetApiStatusAsync(
            new Uri("https://example.test/"), TestContext.Current.CancellationToken));
        Assert.Null(error.Problem);
        Assert.Equal(69632, stream.BytesRead);
    }

    [Fact]
    public async Task CancellationInterruptsProblemStream()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var content = new StreamingContent(new CancelledStream());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content });
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MuscleForgeApiClient(http).GetApiStatusAsync(
            new Uri("https://example.test/"), cancellation.Token));
    }

    [Fact]
    public async Task UndeclaredSuccessStatusIsRejected()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.Accepted, "{}"))));
        var error = await Assert.ThrowsAsync<ApiException>(() => new MuscleForgeApiClient(http).GetApiStatusAsync(
            new Uri("https://example.test/"), TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Accepted, error.StatusCode);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var http = new HttpClient(new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MuscleForgeApiClient(http).GetApiStatusAsync(new Uri("https://example.test/"), cancellation.Token));
    }
}
