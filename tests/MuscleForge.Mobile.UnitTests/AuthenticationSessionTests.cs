using System.Net;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Settings;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public sealed class AuthenticationSessionTests
{
    private static readonly Guid AccountId = Guid.Parse("6ad978d0-0e8b-42f8-884a-3674010cf27e");
    private sealed class Settings : IConnectionSettings { public string ApiBaseUrl { get; set; } = "https://example.test/"; }
    private sealed class Store : IRefreshCredentialStore
    {
        public string? Value; public bool FailRead, FailWrite, FailClear;
        public Func<string, Task>? OnWrite;
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => FailRead ? Task.FromException<string?>(new Exception()) : Task.FromResult(Value);
        public async Task WriteAsync(string value, CancellationToken cancellationToken = default) { if (FailWrite) throw new Exception(); if (OnWrite is not null) await OnWrite(value); Value = value; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { if (FailClear) throw new Exception(); Value = null; return Task.CompletedTask; }
    }
    private sealed class Gateway : IAuthGateway
    {
        public int RefreshCalls; public Guid Id = AccountId;
        public TaskCompletionSource<bool> RefreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task<AuthTokens>> Refresh = () => Task.FromResult(new AuthTokens("access2", "refresh2", 900));
        public Task<AuthTokens> LoginAsync(Uri uri, string email, string password, CancellationToken ct) => Task.FromResult(new AuthTokens("access1", "refresh1", 900));
        public Task<AuthTokens> RefreshAsync(Uri uri, string refreshToken, CancellationToken ct) { Interlocked.Increment(ref RefreshCalls); RefreshStarted.TrySetResult(true); return Refresh(); }
        public Task<AuthAccount> GetCurrentUserAsync(Uri uri, string accessToken, CancellationToken ct) => Task.FromResult(new AuthAccount(Id, "user@example.test"));
    }
    [Fact]
    public async Task SignInPersistsRefreshOnlyAndRestoreChecksIdentity()
    {
        var store = new Store(); var gateway = new Gateway(); var settings = new Settings();
        var session = new AuthSession(settings, store, gateway);
        await session.SignInAsync("user@example.test", "synthetic-password", TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.SignedIn, session.State.Status); Assert.Equal(AccountId, session.State.Account!.Id);
        Assert.Contains("refresh1", store.Value); Assert.DoesNotContain("access1", store.Value); Assert.DoesNotContain("synthetic-password", store.Value);
        var restored = new AuthSession(settings, store, gateway); await restored.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.SignedIn, restored.State.Status); Assert.Equal("access2", (await restored.GetContextAsync(TestContext.Current.CancellationToken))!.AccessToken);
        gateway.Id = Guid.NewGuid(); var mismatch = new AuthSession(settings, store, gateway); await mismatch.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.RequiresSignIn, mismatch.State.Status);
    }
    [Fact]
    public async Task RefreshIsSharedAndStaleRejectionUsesNewToken()
    {
        var gate = new TaskCompletionSource<AuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store(); var gateway = new Gateway { Refresh = () => gate.Task }; var session = new AuthSession(new Settings(), store, gateway);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken); var old = (await session.GetContextAsync(TestContext.Current.CancellationToken))!;
        var calls = Enumerable.Range(0, 20).Select(_ => session.RefreshAsync(old, TestContext.Current.CancellationToken)).ToArray();
        gate.SetResult(new AuthTokens("new-access", "new-refresh", 900)); await Task.WhenAll(calls);
        Assert.Equal(1, gateway.RefreshCalls); Assert.All(calls, task => Assert.Equal("new-access", task.Result!.AccessToken));
        Assert.Equal("new-access", (await session.RefreshAsync(old, TestContext.Current.CancellationToken))!.AccessToken); Assert.Equal(1, gateway.RefreshCalls);
    }
    [Fact]
    public async Task FailedStorageAndInvalidCredentialsNeverExposeSession()
    {
        var store = new Store { FailWrite = true }; var session = new AuthSession(new Settings(), store, new Gateway());
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken); Assert.Equal(AuthStatus.StorageError, session.State.Status); Assert.Null(await session.GetContextAsync(TestContext.Current.CancellationToken));
        store.FailWrite = false; store.FailRead = true; await session.RestoreAsync(TestContext.Current.CancellationToken); Assert.Equal(AuthStatus.StorageError, session.State.Status);
        store.FailRead = false; store.Value = "not json"; await session.RestoreAsync(TestContext.Current.CancellationToken); Assert.Equal(AuthStatus.RequiresSignIn, session.State.Status); Assert.Equal("not json", store.Value);
        store.FailClear = true; await session.SignOutAsync(TestContext.Current.CancellationToken); Assert.Equal(AuthStatus.StorageError, session.State.Status);
    }
    [Fact]
    public async Task LogoutWinsAgainstLateRefreshAndServerMismatchDoesNotSendToken()
    {
        var gate = new TaskCompletionSource<AuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store(); var settings = new Settings(); var gateway = new Gateway { Refresh = () => gate.Task }; var session = new AuthSession(settings, store, gateway);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken); var old = (await session.GetContextAsync(TestContext.Current.CancellationToken))!; var pending = session.RefreshAsync(old, TestContext.Current.CancellationToken);
        await gateway.RefreshStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await session.SignOutAsync(TestContext.Current.CancellationToken); gate.SetResult(new AuthTokens("late", "late-refresh", 900)); Assert.Null(await pending);
        Assert.Equal(AuthStatus.SignedOut, session.State.Status); Assert.Null(store.Value);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken); settings.ApiBaseUrl = "https://other.test/";
        Assert.Null(await session.GetContextAsync(TestContext.Current.CancellationToken)); Assert.Equal(AuthStatus.RequiresSignIn, session.State.Status);
    }
    [Fact]
    public async Task UnauthorizedRefreshIsSharedAndSafe()
    {
        var gate = new TaskCompletionSource<AuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new Gateway { Refresh = () => gate.Task }; var session = new AuthSession(new Settings(), new Store(), gateway);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken); var old = (await session.GetContextAsync(TestContext.Current.CancellationToken))!;
        var calls = Enumerable.Range(0, 10).Select(_ => session.RefreshAsync(old, TestContext.Current.CancellationToken)).ToArray();
        gate.SetException(new ApiException(HttpStatusCode.Unauthorized, null, null)); await Task.WhenAll(calls);
        Assert.Equal(1, gateway.RefreshCalls); Assert.Equal(AuthStatus.RequiresSignIn, session.State.Status); Assert.All(calls, task => Assert.Null(task.Result));
        Assert.DoesNotContain("access1", old.ToString()); Assert.DoesNotContain("refresh", new AuthTokens("access", "refresh", 900).ToString());
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task ExpiryUsesRequestTimeAndSafetyMargin()
    {
        var clock = new FakeClock(); var gateway = new Gateway(); var session = new AuthSession(new Settings(), new Store(), gateway, clock);
        var ct = TestContext.Current.CancellationToken;
        await session.SignInAsync("a", "b", ct);
        clock.Now = clock.Now.AddSeconds(869);
        Assert.Equal("access1", (await session.GetContextAsync(ct))!.AccessToken);
        Assert.Equal(0, gateway.RefreshCalls);
        clock.Now = clock.Now.AddSeconds(1);
        Assert.Equal("access2", (await session.GetContextAsync(ct))!.AccessToken);
        Assert.Equal(1, gateway.RefreshCalls);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotCancelSharedRefresh()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource<AuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new Gateway { Refresh = () => gate.Task }; var session = new AuthSession(new Settings(), new Store(), gateway);
        await session.SignInAsync("a", "b", ct); var old = (await session.GetContextAsync(ct))!;
        using var cancelled = new CancellationTokenSource();
        var first = session.RefreshAsync(old, cancelled.Token); var second = session.RefreshAsync(old, ct);
        await gateway.RefreshStarted.Task.WaitAsync(ct); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        gate.SetResult(new AuthTokens("new-access", "new-refresh", 900));
        Assert.Equal("new-access", (await second)!.AccessToken); Assert.Equal(1, gateway.RefreshCalls);
        Assert.Equal(AuthStatus.SignedIn, session.State.Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await session.GetContextAsync(cancelled.Token));
    }

    [Fact]
    public async Task LogoutDuringCredentialWriteClearsLateWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store { OnWrite = async _ => { entered.SetResult(true); await release.Task; } };
        var session = new AuthSession(new Settings(), store, new Gateway());
        var login = session.SignInAsync("a", "b", ct); await entered.Task.WaitAsync(ct);
        Assert.Null(await session.GetContextAsync(ct));
        var logout = session.SignOutAsync(ct); release.SetResult(true);
        await Task.WhenAll(login, logout);
        Assert.Equal(AuthStatus.SignedOut, session.State.Status); Assert.Null(store.Value); Assert.Null(await session.GetContextAsync(ct));
    }

    [Fact]
    public async Task ServerChangeDuringRefreshDiscardsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource<AuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = new Settings(); var store = new Store(); var gateway = new Gateway { Refresh = () => gate.Task };
        var session = new AuthSession(settings, store, gateway);
        await session.SignInAsync("a", "b", ct); var old = (await session.GetContextAsync(ct))!;
        var pending = session.RefreshAsync(old, ct); await gateway.RefreshStarted.Task.WaitAsync(ct);
        settings.ApiBaseUrl = "https://other.test/"; gate.SetResult(new AuthTokens("late", "late-refresh", 900));
        Assert.Null(await pending); Assert.Equal(AuthStatus.RequiresSignIn, session.State.Status);
        Assert.DoesNotContain("late-refresh", store.Value); Assert.Null(await session.GetContextAsync(ct));
        var other = new AuthSession(settings, store, gateway); await other.RestoreAsync(ct);
        Assert.Equal(AuthStatus.RequiresSignIn, other.State.Status); Assert.Equal(1, gateway.RefreshCalls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"Server\":\"https://example.test/\",\"AccountId\":\"6ad978d0-0e8b-42f8-884a-3674010cf27e\",\"RefreshToken\":\"synthetic\"}")]
    [InlineData("{\"Version\":1,\"Server\":\"http://example.test/\",\"AccountId\":\"6ad978d0-0e8b-42f8-884a-3674010cf27e\",\"RefreshToken\":\"synthetic\"}")]
    public async Task InvalidEnvelopeIsPreservedAndNeverSent(string envelope)
    {
        var ct = TestContext.Current.CancellationToken; var store = new Store { Value = envelope }; var gateway = new Gateway();
        var session = new AuthSession(new Settings(), store, gateway); await session.RestoreAsync(ct);
        Assert.Equal(AuthStatus.RequiresSignIn, session.State.Status); Assert.Equal(envelope, store.Value);
        Assert.Equal(0, gateway.RefreshCalls); Assert.Null(await session.GetContextAsync(ct));
    }

    [Fact]
    public async Task TransientRefreshFailureCanRecoverThroughRestore()
    {
        var ct = TestContext.Current.CancellationToken; var gateway = new Gateway { Refresh = () => Task.FromException<AuthTokens>(new HttpRequestException()) };
        var session = new AuthSession(new Settings(), new Store(), gateway);
        await session.SignInAsync("a", "b", ct); var old = (await session.GetContextAsync(ct))!;
        Assert.Null(await session.RefreshAsync(old, ct)); Assert.Equal(AuthStatus.ConnectionError, session.State.Status);
        gateway.Refresh = () => Task.FromResult(new AuthTokens("recovered", "refresh3", 900));
        await session.RestoreAsync(ct); Assert.Equal("recovered", (await session.GetContextAsync(ct))!.AccessToken);
    }

    [Fact]
    public async Task CancelledWriteThatCommitsIsClearedBeforeReturning()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store { OnWrite = async _ => { entered.SetResult(true); await release.Task; } };
        var session = new AuthSession(new Settings(), store, new Gateway());
        using var cancellation = new CancellationTokenSource();
        var login = session.SignInAsync("a", "b", cancellation.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel(); release.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
        Assert.Null(store.Value); Assert.Equal(AuthStatus.SignedOut, session.State.Status);
        Assert.Null(await session.GetContextAsync(TestContext.Current.CancellationToken));
    }

}
