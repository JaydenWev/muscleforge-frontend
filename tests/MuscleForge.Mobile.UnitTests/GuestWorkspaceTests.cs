using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public sealed class GuestWorkspaceTests
{
    private sealed class Settings : IConnectionSettings { public string ApiBaseUrl { get; set; } = ""; }
    private sealed class GuestStore : IGuestWorkspaceStore
    {
        public string? Value;
        public bool FailWrite;
        public int Writes;
        public Func<Task>? BeforeWrite;
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            Writes++;
            if (BeforeWrite is not null) await BeforeWrite();
            if (FailWrite) throw new IOException();
            Value = value;
        }
    }
    private sealed class LegacyStore : IWorkspaceStore
    {
        public string? Value;
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Value = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Value = null; return Task.CompletedTask; }
    }
    private sealed class Migration : ILegacyTemplateMigration
    {
        public List<(TemplateScope Source, TemplateScope Guest)> Calls { get; } = [];
        public bool Fail;
        public int Conflicts;
        public Task<IReadOnlyList<LegacyTemplateLibrary>> ListLibrariesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LegacyTemplateLibrary>>([]);
        public Task<LegacyTemplateImportResult> ImportLibraryAsync(TemplateScope sourceScope, TemplateScope guestScope, CancellationToken cancellationToken = default)
        {
            Calls.Add((sourceScope, guestScope));
            if (Fail) throw new IOException();
            return Task.FromResult(new LegacyTemplateImportResult(1, 0, Conflicts));
        }
    }
    private static GuestWorkspace Create(GuestStore store, LegacyStore? legacy = null, Settings? settings = null, Migration? migration = null) =>
        new(store, legacy ?? new(), settings ?? new(), migration ?? new());

    [Fact]
    public async Task FreshOfflineWorkspacePersistsBeforePublishingAndSurvivesRestart()
    {
        var store = new GuestStore(); var workspace = Create(store);
        workspace.Changed += (_, _) => Assert.NotNull(store.Value);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(workspace.IsReady);
        Assert.True(workspace.CurrentScope!.IsGuest);
        Assert.Equal("local://device/", workspace.CurrentScope.Server.AbsoluteUri);
        var reopened = Create(store);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(workspace.CurrentScope, reopened.CurrentScope);
        Assert.Equal(1, store.Writes);
        Assert.Throws<TemplateValidationException>(() => TemplateScope.Create("local://device/", Guid.NewGuid()));
    }

    [Fact]
    public async Task GuestIdentitySurvivesLegacyAccountServerSignOutChangesAndReopen()
    {
        var guestStore = new GuestStore();
        var settings = new Settings { ApiBaseUrl = "https://first.example.test/" };
        var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://first.example.test/","AccountId":"{{Guid.NewGuid()}}"}""" };
        var migration = new Migration();

        var first = Create(guestStore, legacy, settings, migration);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        var originalScope = first.CurrentScope;
        Assert.NotNull(originalScope);

        settings.ApiBaseUrl = "https://second.example.test/";
        legacy.Value = $$"""{"Version":1,"Server":"https://second.example.test/","AccountId":"{{Guid.NewGuid()}}"}""";
        var changedAccountAndServer = Create(guestStore, legacy, settings, migration);
        await changedAccountAndServer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalScope, changedAccountAndServer.CurrentScope);

        settings.ApiBaseUrl = "";
        legacy.Value = $$"""{"Version":2,"Server":"https://second.example.test/","AccountId":"{{Guid.NewGuid()}}"}""";
        var signedOut = Create(guestStore, legacy, settings, migration);
        await signedOut.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalScope, signedOut.CurrentScope);

        var reopened = Create(guestStore, legacy, settings, migration);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalScope, reopened.CurrentScope);
        Assert.Equal(1, guestStore.Writes);
    }

    [Fact]
    public async Task ImportsOnlyMatchingRememberedVerifiedScope()
    {
        var account = Guid.NewGuid();
        var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://example.test/","AccountId":"{{account}}"}""" };
        var migration = new Migration(); var store = new GuestStore();
        var workspace = Create(store, legacy, new Settings { ApiBaseUrl = "https://example.test" }, migration);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        var call = Assert.Single(migration.Calls);
        Assert.Equal(account, call.Source.AccountId);
        Assert.Equal(workspace.CurrentScope, call.Guest);
        Assert.Contains(account.ToString(), legacy.Value);
    }

    [Fact]
    public async Task AutomaticImportConflictsExposeNoticeAndLeaveWorkspaceReady()
    {
        var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://example.test/","AccountId":"{{Guid.NewGuid()}}"}""" };
        var original = legacy.Value;
        var migration = new Migration { Conflicts = 1 };
        var workspace = Create(new(), legacy, new Settings { ApiBaseUrl = "https://example.test" }, migration);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(workspace.IsReady);
        Assert.NotNull(workspace.CurrentScope);
        Assert.Null(workspace.Error);
        Assert.Equal("Some saved templates could not be imported because IDs conflict. Your original library is unchanged; review Existing libraries in Settings.", workspace.Notice);
        Assert.Equal(original, legacy.Value);
    }

    [Theory]
    [InlineData(2, "https://example.test/")]
    [InlineData(1, "https://other.test/")]
    public async Task SignedOutOrDifferentServerDoesNotAutoImport(int version, string server)
    {
        var legacy = new LegacyStore { Value = $$"""{"Version":{{version}},"Server":"https://example.test/","AccountId":"{{Guid.NewGuid()}}"}""" };
        var migration = new Migration(); var workspace = Create(new(), legacy, new Settings { ApiBaseUrl = server }, migration);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(workspace.IsReady);
        Assert.Empty(migration.Calls);
    }

    [Fact]
    public async Task FailedPersistenceAndMalformedIdentityNeverPublishOrReplaceIdentity()
    {
        var store = new GuestStore { FailWrite = true }; var workspace = Create(store);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.False(workspace.IsReady); Assert.NotNull(workspace.Error); Assert.Null(store.Value);
        store.FailWrite = false;
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(workspace.IsReady); Assert.Null(workspace.Error);
        store.Value = "bad identity";
        var malformed = Create(store); var writes = store.Writes;
        await malformed.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.False(malformed.IsReady); Assert.NotNull(malformed.Error);
        Assert.Equal("bad identity", store.Value); Assert.Equal(writes, store.Writes);
    }

    [Fact]
    public async Task ImportFailureRetriesWithSamePersistedIdentity()
    {
        var store = new GuestStore(); var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://example.test/","AccountId":"{{Guid.NewGuid()}}"}""" };
        var migration = new Migration { Fail = true };
        var workspace = Create(store, legacy, new Settings { ApiBaseUrl = "https://example.test" }, migration);
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.False(workspace.IsReady); Assert.NotNull(workspace.Error);
        var saved = store.Value;
        migration.Fail = false;
        await workspace.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(workspace.IsReady); Assert.Equal(saved, store.Value);
        Assert.Equal(migration.Calls[0].Guest, migration.Calls[1].Guest);
    }

    [Fact]
    public async Task EstablishedGuestDoesNotAutoImportAnAccountAddedLater()
    {
        var store = new GuestStore(); var legacy = new LegacyStore(); var settings = new Settings();
        var initial = Create(store, legacy, settings);
        await initial.InitializeAsync(TestContext.Current.CancellationToken);
        var migration = new Migration();
        settings.ApiBaseUrl = "https://other.test";
        legacy.Value = $$"""{"Version":1,"Server":"https://other.test/","AccountId":"{{Guid.NewGuid()}}"}""";
        var reopened = Create(store, legacy, settings, migration);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(initial.CurrentScope, reopened.CurrentScope);
        Assert.Empty(migration.Calls);
    }

    [Fact]
    public async Task ChangedAccountDoesNotReplaceBoundAutomaticSource()
    {
        var originalAccount = Guid.NewGuid();
        var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://first.test/","AccountId":"{{originalAccount}}"}""" };
        var store = new GuestStore(); var settings = new Settings { ApiBaseUrl = "https://first.test" }; var migration = new Migration();
        var first = Create(store, legacy, settings, migration);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        settings.ApiBaseUrl = "https://other.test";
        legacy.Value = $$"""{"Version":1,"Server":"https://other.test/","AccountId":"{{Guid.NewGuid()}}"}""";
        var reopened = Create(store, legacy, settings, migration);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(reopened.IsReady);
        Assert.Equal(first.CurrentScope, reopened.CurrentScope);
        Assert.Equal(2, migration.Calls.Count);
        Assert.All(migration.Calls, call => Assert.Equal(originalAccount, call.Source.AccountId));
    }

    [Fact]
    public async Task FailedImportRetriesBoundSourceEvenAfterLegacyAccountChanges()
    {
        var originalAccount = Guid.NewGuid();
        var legacy = new LegacyStore { Value = $$"""{"Version":1,"Server":"https://first.test/","AccountId":"{{originalAccount}}"}""" };
        var store = new GuestStore(); var settings = new Settings { ApiBaseUrl = "https://first.test" }; var migration = new Migration { Fail = true };
        var first = Create(store, legacy, settings, migration);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.False(first.IsReady);
        var persisted = store.Value;
        settings.ApiBaseUrl = "https://other.test";
        legacy.Value = $$"""{"Version":1,"Server":"https://other.test/","AccountId":"{{Guid.NewGuid()}}"}""";
        migration.Fail = false;
        var retry = Create(store, legacy, settings, migration);
        await retry.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(retry.IsReady);
        Assert.Equal(persisted, store.Value);
        Assert.Equal(2, migration.Calls.Count);
        Assert.All(migration.Calls, call => Assert.Equal(originalAccount, call.Source.AccountId));
        Assert.Equal(migration.Calls[0].Guest, retry.CurrentScope);
    }

    [Fact]
    public async Task ConcurrentInitializationPublishesOneIdentity()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new GuestStore { BeforeWrite = async () => { entered.SetResult(); await release.Task; } };
        var workspace = Create(store);
        var first = workspace.InitializeAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        var waiting = workspace.InitializeAsync(cancelled.Token);
        var others = Enumerable.Range(0, 20).Select(_ => workspace.InitializeAsync(TestContext.Current.CancellationToken)).ToArray();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Null(workspace.CurrentScope);
        release.SetResult();
        await Task.WhenAll(others.Append(first));
        Assert.True(workspace.IsReady); Assert.Equal(1, store.Writes);
    }
}
