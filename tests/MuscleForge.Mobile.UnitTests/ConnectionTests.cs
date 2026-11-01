using System.Net;
using System.Net.Http.Json;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.ViewModels;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public sealed class ConnectionTests
{
    [Theory]
    [InlineData("http://localhost:5187")]
    [InlineData("https://user:password@example.test/")]
    [InlineData("https://example.test/?token=secret")]
    [InlineData("https://example.test/#fragment")]
    [InlineData("")]
    public void InvalidApiAddressesAreRejected(string input) => Assert.False(ApiEndpoint.TryParse(input, out _));

    [Fact]
    public async Task GeneratedClientRetainsTheConfiguredPathPrefix()
    {
        using var handler = new StatusHandler();
        using var http = new HttpClient(handler);
        var client = new MuscleForgeApiClient(http);
        var response = await client.GetApiStatusAsync(new Uri("https://example.test/muscleforge/"));
        Assert.Equal("https://example.test/muscleforge/api/v1/status", handler.RequestUri!.AbsoluteUri);
        Assert.Equal("MuscleForge", response.Service);
    }

    [Fact]
    public async Task FailedConnectionDoesNotReportSuccessAndCanBeRetried()
    {
        var api = new FakeApi { Fails = true };
        var settings = new FakeSettings { ApiBaseUrl = "https://example.test" };
        var vm = new ConnectionViewModel(api, settings);
        await vm.CheckConnectionCommand.ExecuteAsync(null);
        Assert.StartsWith("Could not reach", vm.Status);
        Assert.False(vm.IsBusy);
        api.Fails = false;
        await vm.CheckConnectionCommand.ExecuteAsync(null);
        Assert.Equal("Connected to MuscleForge API v1.", vm.Status);
        Assert.Equal("https://example.test/", settings.ApiBaseUrl);
    }

    [Fact]
    public async Task InvalidAddressDoesNotSendARequestOrSaveSettings()
    {
        var api = new FakeApi();
        var settings = new FakeSettings { ApiBaseUrl = "https://example.test/" };
        var vm = new ConnectionViewModel(api, settings) { ApiBaseUrl = "http://example.test" };
        await vm.CheckConnectionCommand.ExecuteAsync(null);
        Assert.Equal(0, api.Calls);
        Assert.Equal("https://example.test/", settings.ApiBaseUrl);
    }

    [Fact]
    public async Task IncompatibleApiVersionIsReported()
    {
        var vm = new ConnectionViewModel(new FakeApi { Version = "v2" },
            new FakeSettings { ApiBaseUrl = "https://example.test/" });
        await vm.CheckConnectionCommand.ExecuteAsync(null);
        Assert.Equal("This server is not a compatible MuscleForge API.", vm.Status);
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ApiStatusResponse { Service = "MuscleForge", ApiVersion = "v1" })
            });
        }
    }

    private sealed class FakeSettings : IConnectionSettings
    {
        public string ApiBaseUrl { get; set; } = string.Empty;
    }

    private sealed class FakeApi : IApiStatusService
    {
        public bool Fails { get; set; }
        public int Calls { get; private set; }
        public string Version { get; set; } = "v1";

        public Task<ApiStatusResponse> GetStatusAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            Calls++;
            return Fails
                ? Task.FromException<ApiStatusResponse>(new HttpRequestException("Unavailable"))
                : Task.FromResult(new ApiStatusResponse { Service = "MuscleForge", ApiVersion = Version });
        }
    }
}
