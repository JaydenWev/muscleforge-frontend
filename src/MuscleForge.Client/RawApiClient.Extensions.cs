using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

namespace MuscleForge.Client;

public partial class RawApiClient
{
    internal string? BearerToken { get; init; }

    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        if (BearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
    }

    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.NumberHandling = JsonNumberHandling.AllowReadingFromString;
}
