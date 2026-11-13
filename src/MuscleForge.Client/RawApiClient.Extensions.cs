using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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

    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        settings.TypeInfoResolver = JsonSerializerOptions.Default.TypeInfoResolver!.WithAddedModifier(typeInfo =>
        {
            if (typeInfo.Type != typeof(AccountPreferencesResponse)) return;
            foreach (var property in typeInfo.Properties)
                if (property.Name is "weightUnit" or "timeZoneId") property.IsRequired = true;
        });
    }
}
