using System.Net;
using System.Text;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class CatalogGatewayTests
{
    [Fact]
    public async Task Unauthorized_refreshes_once_and_uses_same_bound_account()
    {
        var session = new Session(); var tokens = new List<string?>(); var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler((r, _) => { tokens.Add(r.Headers.Authorization?.Parameter); return Task.FromResult(tokens.Count == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(id)); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        var selection = await gateway.SelectAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "old", "new" }, tokens); Assert.Equal(1, session.Refreshes); Assert.Equal(session.Account, selection!.AccountId); Assert.Equal(id, selection.ExerciseId);
    }
    [Fact]
    public async Task Unauthorized_does_not_retry_under_a_different_account()
    {
        var session = new Session { ChangeOnRefresh = true }; var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Ambiguous_write_failure_is_never_retried()
    {
        var session = new Session(); var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; throw new HttpRequestException("network"); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.CreateAsync(new("Press", "Barbell", ExerciseLoadConvention.TotalLoad), TestContext.Current.CancellationToken));
        Assert.Equal(1, calls); Assert.Equal(0, session.Refreshes);
    }
    [Fact]
    public async Task Completion_after_server_change_is_rejected()
    {
        var session = new Session(); var pending = new TaskCompletionSource<HttpResponseMessage>(); var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler((_, _) => pending.Task));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        var request = gateway.GetAsync(id, TestContext.Current.CancellationToken);
        session.Server = new Uri("https://other.example.test/"); pending.SetResult(Json(id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }
    [Fact]
    public async Task Second_unauthorized_does_not_refresh_again()
    {
        var session = new Session(); var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAsync<ApiException>(() => gateway.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal(2, calls); Assert.Equal(1, session.Refreshes);
    }
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task Client_errors_do_not_retry_writes(HttpStatusCode status)
    {
        var session = new Session(); var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(status)); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAsync<ApiException>(() => gateway.CreateAsync(new("Press", "Barbell", ExerciseLoadConvention.TotalLoad), TestContext.Current.CancellationToken));
        Assert.Equal(1, calls); Assert.Equal(0, session.Refreshes);
    }
    [Fact]
    public async Task Update_rejects_response_for_another_exercise()
    {
        var session = new Session();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Guid.NewGuid()))));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), session);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.UpdateAsync(Guid.NewGuid(),
            new("Press", "Barbell", ExerciseLoadConvention.TotalLoad), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Create_rejects_archived_draft_without_request()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Json(Guid.NewGuid())); }));
        var gateway = new ApiExerciseCatalogGateway(new MuscleForgeApiClient(http), new Session());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gateway.CreateAsync(
            new("Press", "Barbell", ExerciseLoadConvention.TotalLoad, true), TestContext.Current.CancellationToken));
        Assert.Equal(0, calls);
    }
    private static HttpResponseMessage Json(Guid id) => new(HttpStatusCode.OK) { Content = new StringContent($$"""{"id":"{{id}}","name":"Press","equipment":"Barbell","trackingType":"ExternalWeightAndReps","loadConvention":"TotalLoad","isArchived":false,"archivedAt":null}""", Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
    private sealed class Session : IAuthSession
    {
        public Uri Server = new("https://example.test/"); public Guid Account = Guid.NewGuid(); public int Refreshes; public bool ChangeOnRefresh; private string token = "old";
        public SessionState State => new(AuthStatus.SignedIn, new(Account, "user@example.com"));
        public event EventHandler<SessionState>? StateChanged { add { } remove { } }
        public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(new(Server, Account, token));
        public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default) { Refreshes++; token = "new"; if (ChangeOnRefresh) Account = Guid.NewGuid(); return GetContextAsync(cancellationToken); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string email, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
