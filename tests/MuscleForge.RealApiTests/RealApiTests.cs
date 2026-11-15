using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Settings;
using Xunit;

public sealed class RealApiTests
{
    [Fact]
    public async Task SessionAndCatalogWorkThroughTheRealApi()
    {
        var ct = TestContext.Current.CancellationToken;
        var url = Required("MF_TEST_API_URL");
        var email = Required("MF_TEST_EMAIL");
        var password = Required("MF_TEST_PASSWORD");
        using var http = new HttpClient();
        var client = new MuscleForgeApiClient(http);
        var settings = new Settings { ApiBaseUrl = url };
        var store = new Store();
        var gateway = new ApiAuthGateway(client);
        var session = new AuthSession(settings, store, gateway);
        await session.SignInAsync(email, password, ct);
        Assert.Equal(AuthStatus.SignedIn, session.State.Status);
        var account = session.State.Account!;
        Assert.Equal(email, account.Email);
        var restarted = new AuthSession(settings, store, gateway);
        await restarted.RestoreAsync(ct);
        Assert.Equal(account, restarted.State.Account);
        var context = await restarted.GetContextAsync(ct);
        var renewed = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => restarted.RefreshAsync(context!, ct)));
        Assert.All(renewed, item => Assert.Equal(account.Id, item!.AccountId));
        Assert.True(renewed.Select(item => item!.AccessToken).Distinct().Count() == 1);

        var catalog = new ApiExerciseCatalogGateway(client, restarted);
        using var list = new ExerciseListViewModel(catalog, restarted);
        await list.LoadAsync(ct);
        Assert.True(list.Items.Count >= 11);
        using var editor = new ExerciseEditorViewModel(catalog, restarted)
        {
            Name = "Verification " + Guid.NewGuid(), Equipment = "Dumbbells",
            LoadConvention = ExerciseLoadConvention.PerDumbbell
        };
        Assert.True(await editor.SaveAsync(ct));
        var created = editor.SavedExercise!;
        list.SearchText = created.Name;
        await list.LoadAsync(ct);
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
        using var existingEditor = new ExerciseEditorViewModel(catalog, restarted, created);
        await existingEditor.LoadAsync(ct);
        existingEditor.Name = created.Name + " edited";
        Assert.True(await existingEditor.SaveAsync(ct));
        list.SearchText = existingEditor.Name;
        await list.LoadAsync(ct);
        Assert.Equal(existingEditor.Name, Assert.Single(list.Items).Name);
        Assert.True(await existingEditor.ArchiveAsync(ct));
        await list.LoadAsync(ct);
        Assert.Empty(list.Items);
        list.IncludeArchived = true;
        await list.LoadAsync(ct);
        Assert.True(Assert.Single(list.Items).IsArchived);
        await restarted.SignOutAsync(ct);
        Assert.Null(store.Value);
        Assert.Equal(AuthStatus.SignedOut, restarted.State.Status);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Set {name} for the isolated synthetic test server.");

    private sealed class Settings : IConnectionSettings { public string ApiBaseUrl { get; set; } = ""; }
    private sealed class Store : IRefreshCredentialStore
    {
        public string? Value { get; private set; }
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Value = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Value = null; return Task.CompletedTask; }
    }
}
