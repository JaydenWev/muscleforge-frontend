using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public sealed class LocalWorkspaceTests
{
    private static readonly Guid Account = Guid.Parse("6ad978d0-0e8b-42f8-884a-3674010cf27e");
    private sealed class Settings : IConnectionSettings { public string ApiBaseUrl { get; set; } = "https://example.test"; }
    private sealed class WorkspaceStore : IWorkspaceStore
    {
        public string? Value;
        public bool FailWrite, FailClear;
        public Func<Task>? BeforeWrite;
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            if (BeforeWrite is not null) await BeforeWrite();
            if (FailWrite || (FailClear && value.Contains("\"Version\":2", StringComparison.Ordinal))) throw new IOException();
            Value = value;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (FailClear) throw new IOException();
            Value = null;
            return Task.CompletedTask;
        }
    }
    private sealed class CredentialStore : IRefreshCredentialStore
    {
        public string? Value;
        public bool FailClear;
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Value = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { if (FailClear) throw new IOException(); Value = null; return Task.CompletedTask; }
    }
    private sealed class Gateway : IAuthGateway
    {
        public Guid Id = Account;
        public bool Offline;
        public Task<AuthTokens> LoginAsync(Uri baseUri, string email, string password, CancellationToken cancellationToken) => Task.FromResult(new AuthTokens("access", "refresh", 900));
        public Task<AuthTokens> RefreshAsync(Uri baseUri, string refreshToken, CancellationToken cancellationToken) => Offline ? Task.FromException<AuthTokens>(new HttpRequestException()) : Task.FromResult(new AuthTokens("access", "refresh", 900));
        public Task<AuthAccount> GetCurrentUserAsync(Uri baseUri, string accessToken, CancellationToken cancellationToken) => Task.FromResult(new AuthAccount(Id, "user@example.test"));
    }

    [Fact]
    public async Task OfflineReopenHasScopeButNeverAnApiContextAndLogoutIsDurable()
    {
        var settings = new Settings(); var saved = new WorkspaceStore(); var credentials = new CredentialStore(); var gateway = new Gateway();
        var firstWorkspace = new LocalWorkspace(saved, settings);
        var first = new AuthSession(settings, credentials, gateway, workspace: firstWorkspace);
        await first.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        Assert.Equal(Account, firstWorkspace.CurrentScope!.AccountId);
        Assert.DoesNotContain("refresh", saved.Value);
        var reopenedWorkspace = new LocalWorkspace(saved, settings);
        await reopenedWorkspace.RestoreAsync(TestContext.Current.CancellationToken);
        gateway.Offline = true;
        var reopened = new AuthSession(settings, credentials, gateway, workspace: reopenedWorkspace);
        await reopened.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.ConnectionError, reopened.State.Status);
        Assert.Equal(Account, reopenedWorkspace.CurrentScope!.AccountId);
        Assert.Null(await reopened.GetContextAsync(TestContext.Current.CancellationToken));
        await reopened.SignOutAsync(TestContext.Current.CancellationToken);
        var afterLogout = new LocalWorkspace(saved, settings);
        await afterLogout.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Null(afterLogout.CurrentScope);
        Assert.Null(credentials.Value);
    }

    [Fact]
    public async Task ServerAndAccountSwitchesUseOnlyTheVerifiedScope()
    {
        var settings = new Settings(); var saved = new WorkspaceStore(); var workspace = new LocalWorkspace(saved, settings); var gateway = new Gateway();
        var session = new AuthSession(settings, new CredentialStore(), gateway, workspace: workspace);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        settings.ApiBaseUrl = "https://other.test/";
        Assert.Null(workspace.CurrentScope);
        gateway.Id = Guid.NewGuid();
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        Assert.Equal(gateway.Id, workspace.CurrentScope!.AccountId);
        Assert.Equal("https://other.test/", workspace.CurrentScope.Server.AbsoluteUri);
        settings.ApiBaseUrl = "https://example.test/";
        Assert.Null(workspace.CurrentScope);
    }

    [Fact]
    public async Task SignOutWinsWhenWorkspaceWriteCompletesLate()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new WorkspaceStore { BeforeWrite = async () => { entered.TrySetResult(); await release.Task; } };
        var settings = new Settings(); var workspace = new LocalWorkspace(saved, settings); var credentials = new CredentialStore();
        var session = new AuthSession(settings, credentials, new Gateway(), workspace: workspace);
        var login = session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var logout = session.SignOutAsync(TestContext.Current.CancellationToken);
        release.SetResult();
        await Task.WhenAll(login, logout);
        Assert.Equal(AuthStatus.SignedOut, session.State.Status);
        Assert.Null(workspace.CurrentScope); Assert.NotNull(saved.Value); Assert.Null(credentials.Value);
    }

    [Fact]
    public async Task PersistenceFailuresDoNotReportSuccessfulSignInOrSignOut()
    {
        var settings = new Settings(); var saved = new WorkspaceStore { FailWrite = true }; var workspace = new LocalWorkspace(saved, settings);
        var session = new AuthSession(settings, new CredentialStore(), new Gateway(), workspace: workspace);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.StorageError, session.State.Status); Assert.Null(workspace.CurrentScope);
        saved.FailWrite = false;
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        saved.FailClear = true;
        await session.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.StorageError, session.State.Status); Assert.Null(workspace.CurrentScope);
    }
    [Fact]
    public async Task TombstoneBlocksRestoreWhenCredentialClearFailsAndManualLoginReenablesWorkspace()
    {
        var settings = new Settings(); var saved = new WorkspaceStore(); var credentials = new CredentialStore(); var gateway = new Gateway();
        var workspace = new LocalWorkspace(saved, settings);
        var session = new AuthSession(settings, credentials, gateway, workspace: workspace);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        credentials.FailClear = true;
        await session.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.StorageError, session.State.Status);
        Assert.Null(workspace.CurrentScope); Assert.NotNull(credentials.Value);
        var reopenedWorkspace = new LocalWorkspace(saved, settings);
        var reopened = new AuthSession(settings, credentials, gateway, workspace: reopenedWorkspace);
        await reopened.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.SignedOut, reopened.State.Status);
        Assert.Null(await reopened.GetContextAsync(TestContext.Current.CancellationToken));
        Assert.Null(reopenedWorkspace.CurrentScope);
        credentials.FailClear = false;
        await reopened.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.SignedIn, reopened.State.Status);
        Assert.Equal(Account, reopenedWorkspace.CurrentScope!.AccountId);
    }

    [Fact]
    public async Task TombstoneFailureStillAttemptsCredentialClear()
    {
        var settings = new Settings(); var saved = new WorkspaceStore(); var credentials = new CredentialStore();
        var workspace = new LocalWorkspace(saved, settings);
        var session = new AuthSession(settings, credentials, new Gateway(), workspace: workspace);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        saved.FailClear = true;
        await session.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.StorageError, session.State.Status);
        Assert.Null(workspace.CurrentScope);
        Assert.Null(credentials.Value);
    }

    [Fact]
    public async Task CancelledLogoutWhileWriteIsBlockedSuspendsImmediatelyAndCompletesCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = new WorkspaceStore(); var settings = new Settings(); var workspace = new LocalWorkspace(saved, settings);
        var session = new AuthSession(settings, new CredentialStore(), new Gateway(), workspace: workspace);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        saved.BeforeWrite = async () => { entered.TrySetResult(); await release.Task; };
        var login = session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        var logout = session.SignOutAsync(cancelled.Token);
        cancelled.Cancel();
        Assert.Null(workspace.CurrentScope);
        saved.BeforeWrite = null;
        release.SetResult();
        await Task.WhenAll(login, logout);
        Assert.Equal(AuthStatus.SignedOut, session.State.Status);
        Assert.Null(workspace.CurrentScope);
        var reopened = new LocalWorkspace(saved, settings);
        await reopened.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.True(reopened.IsExplicitlySignedOut);
    }

    [Fact]
    public async Task OnlyVerifiedSignedInStateExposesServer()
    {
        var settings = new Settings(); var gateway = new Gateway();
        var session = new AuthSession(settings, new CredentialStore(), gateway);
        Assert.Null(session.State.Server);
        await session.SignInAsync("a", "b", TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.SignedIn, session.State.Status);
        Assert.Equal(new Uri("https://example.test/"), session.State.Server);
        gateway.Offline = true;
        var context = await session.GetContextAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(context);
        await session.RefreshAsync(context!, TestContext.Current.CancellationToken);
        Assert.Equal(AuthStatus.ConnectionError, session.State.Status);
        Assert.Null(session.State.Server);
        await session.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.Null(session.State.Server);
    }

}
