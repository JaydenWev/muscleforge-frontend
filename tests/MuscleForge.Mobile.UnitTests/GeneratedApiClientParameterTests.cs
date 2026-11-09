using System.Net;
using System.Text.Json;
using Xunit;
using Fixture = MuscleForge.Client.Fixture;

namespace MuscleForge.Mobile.UnitTests;

public partial class GeneratedApiClientTests
{
    [Fact]
    public async Task FixtureSendsEscapedRequestAndReadsTypedResponse()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/prefix/api/v1/contract-fixture/items/a%2Fb%20%3F?limit=7&filter=x%26y&dryRun=true", request.RequestUri!.PathAndQuery);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("standard", body.RootElement.GetProperty("mode").GetString());
            Assert.False(body.RootElement.TryGetProperty("optionalLabel", out _));
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("note").ValueKind);
            Assert.True(body.RootElement.GetProperty("details").GetProperty("enabled").GetBoolean());
            return Json(HttpStatusCode.OK, $"{{\"id\":\"{id}\",\"count\":3,\"createdAt\":\"2026-01-02T03:04:05Z\"}}");
        }));
        var client = new Fixture.MuscleForgeApiClient(http);
        var result = await client.PutFixtureItemAsync(new Uri("https://example.test/prefix/"), "test-token", new Fixture.PutFixtureItemRequest
        {
            Name = "item", Mode = Fixture.PutFixtureItemRequestMode.Standard,
            Details = new Fixture.PutFixtureItemRequestDetails { Count = 3, Enabled = true, Tags = ["one"] }
        }, "a/b ?", 7, "x&y", true, TestContext.Current.CancellationToken);
        Assert.Equal(id, result.Id);
        Assert.Equal(3, result.Count);
        Assert.Equal(2026, result.CreatedAt.Year);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

}
