using System.Net;
using System.Text;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Preferences;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class AccountPreferencesGatewayTests
{
    [Fact]
    public async Task SavesOriginalScopeAndRetriesUnauthorizedOnce()
    {
        var session = new Session();
        var tokens = new List<string?>();
        var bodies = new List<string>();
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return tokens.Count == 1 ? new(HttpStatusCode.Unauthorized) : Json("""{"weightUnit":"lb","timeZoneId":"Europe/Amsterdam"}""");
        }));
        var result = await new ApiAccountPreferencesGateway(new MuscleForgeApiClient(http), session).SaveAsync(session.Scope, new(WeightUnit.Lb, "Europe/Amsterdam"), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "old", "new" }, tokens);
        Assert.Equal(1, session.Refreshes);
        Assert.All(bodies, body => { Assert.Contains("\"weightUnit\":\"lb\"", body); Assert.Contains("Europe/Amsterdam", body); });
        Assert.Equal(new(WeightUnit.Lb, "Europe/Amsterdam"), result);
    }

    [Fact]
    public async Task RejectsChangedScopeBeforeRequestAndAfterRefresh()
    {
        var session = new Session();
        var original = session.Scope;
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }));
        var gateway = new ApiAccountPreferencesGateway(new MuscleForgeApiClient(http), session);
        session.Server = new("https://other.example.test/");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.SaveAsync(original, new(WeightUnit.Kg, "Etc/UTC"), TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
        session.Server = original.BaseUri;
        session.ChangeOnRefresh = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.SaveAsync(original, new(WeightUnit.Kg, "Etc/UTC"), TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task NetworkFailureDoesNotRetrySave()
    {
        var session = new Session();
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; throw new HttpRequestException(); }));
        var gateway = new ApiAccountPreferencesGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.SaveAsync(session.Scope, new(WeightUnit.Kg, "Etc/UTC"), TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
        Assert.Equal(0, session.Refreshes);
    }

    [Fact]
    public async Task RejectsLateResponseAfterAccountChange()
    {
        var session = new Session();
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        using var http = new HttpClient(new Handler((_, _) => pending.Task));
        var request = new ApiAccountPreferencesGateway(new MuscleForgeApiClient(http), session).GetAsync(session.Scope, TestContext.Current.CancellationToken);
        session.Account = Guid.NewGuid();
        pending.SetResult(Json("""{"weightUnit":"kg","timeZoneId":"Etc/UTC"}"""));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Theory]
    [InlineData("""{"weightUnit":9,"timeZoneId":"Etc/UTC"}""")]
    [InlineData("""{"weightUnit":"kg","timeZoneId":null}""")]
    [InlineData("""{"weightUnit":"kg"}""")]
    [InlineData("""{"weightUnit":"lb","timeZoneId":" "}""")]
    public async Task RejectsMalformedResponses(string body)
    {
        var session = new Session();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body))));
        await Assert.ThrowsAnyAsync<Exception>(() => new ApiAccountPreferencesGateway(new MuscleForgeApiClient(http), session).GetAsync(session.Scope, TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
    private sealed class Session : IAuthSession
    {
        public Uri Server = new("https://example.test/");
        public Guid Account = Guid.NewGuid();
        public int Refreshes;
        public bool ChangeOnRefresh;
        private string token = "old";
        public PreferencesScope Scope => new(Server, Account);
        public SessionState State => new(AuthStatus.SignedIn, new(Account, "user@example.test"));
        public event EventHandler<SessionState>? StateChanged { add { } remove { } }
        public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(new(Server, Account, token));
        public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default) { Refreshes++; token = "new"; if (ChangeOnRefresh) Account = Guid.NewGuid(); return GetContextAsync(cancellationToken); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string email, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
