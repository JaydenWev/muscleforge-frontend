using System.Net;
using System.Text;
using System.Text.Json;
using MuscleForge.Client;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public sealed class PreferencesRequiredResponseTests
{
    [Theory]
    [InlineData("""{"timeZoneId":"Etc/UTC"}""")]
    [InlineData("""{"weightUnit":"kg"}""")]
    public async Task MissingRequiredMemberIsRejected(string json)
    {
        using var http = new HttpClient(new ResponseHandler(json));
        var client = new MuscleForgeApiClient(http);
        await Assert.ThrowsAsync<JsonException>(() => client.GetAccountPreferencesAsync(new Uri("https://example.test/"), "synthetic", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PresentKgEnumValueIsAccepted()
    {
        using var http = new HttpClient(new ResponseHandler("""{"weightUnit":"kg","timeZoneId":"Etc/UTC"}"""));
        var result = await new MuscleForgeApiClient(http).GetAccountPreferencesAsync(new Uri("https://example.test/"), "synthetic", TestContext.Current.CancellationToken);
        Assert.Equal(AccountPreferencesResponseWeightUnit.Kg, result.WeightUnit);
        Assert.Equal("Etc/UTC", result.TimeZoneId);
    }

    private sealed class ResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
