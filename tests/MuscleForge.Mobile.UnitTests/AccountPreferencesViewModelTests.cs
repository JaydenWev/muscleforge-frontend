using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Preferences;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class AccountPreferencesViewModelTests
{
    [Fact]
    public async Task LoadAndSaveUseAccountPreferences()
    {
        var session = new FakeSession();
        var gateway = new FakeGateway();
        using var vm = new AccountPreferencesViewModel(gateway, session, action => action());
        await vm.LoadAsync();
        Assert.Equal("kg", vm.Unit);
        Assert.Equal("Etc/UTC", vm.TimeZoneId);
        vm.Unit = "lb";
        vm.TimeZoneId = "Europe/Amsterdam";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new(WeightUnit.Lb, "Europe/Amsterdam"), gateway.Saved);
        Assert.Equal("Preferences saved.", vm.Message);
    }

    [Fact]
    public async Task FailedSaveRetainsEditsAndReportsFailure()
    {
        var gateway = new FakeGateway();
        using var vm = new AccountPreferencesViewModel(gateway, new FakeSession(), action => action());
        await vm.LoadAsync();
        gateway.FailSave = true;
        vm.TimeZoneId = "Europe/Amsterdam";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Europe/Amsterdam", vm.TimeZoneId);
        Assert.Contains("Could not save", vm.Message);
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task PhoneTimeZoneIsOnlyAnEditUntilSaved()
    {
        var gateway = new FakeGateway();
        using var vm = new AccountPreferencesViewModel(gateway, new FakeSession(), action => action());
        await vm.LoadAsync();
        vm.UsePhoneTimeZoneCommand.Execute(null);
        Assert.Equal(TimeZoneInfo.Local.Id, vm.TimeZoneId);
        Assert.Null(gateway.Saved);
    }

    [Fact]
    public async Task AccountChangeDiscardsLateLoad()
    {
        var session = new FakeSession();
        var gateway = new FakeGateway { Pending = new() };
        using var vm = new AccountPreferencesViewModel(gateway, session, action => action());
        var load = vm.LoadAsync();
        session.SignOut();
        gateway.Pending.SetResult(new(WeightUnit.Lb, "Europe/Amsterdam"));
        await load;
        Assert.Equal("", vm.TimeZoneId);
        Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task SessionChangeDuringContextLookupNeverStartsRequest()
    {
        var session = new FakeSession { PendingContext = new() };
        var gateway = new FakeGateway();
        Action? pendingUi = null;
        using var vm = new AccountPreferencesViewModel(gateway, session, action => pendingUi = action);
        var load = vm.LoadAsync();
        var originalId = session.State.Account!.Id;
        session.SignOut();
        session.PendingContext.SetResult(new(new Uri("https://example.test/"), originalId, "token"));
        await load;
        Assert.False(gateway.LoadCalled);
        pendingUi?.Invoke();
        Assert.False(vm.CanEdit);
    }

    private sealed class FakeGateway : IAccountPreferencesGateway
    {
        public AccountPreferences? Saved;
        public bool FailSave;
        public bool LoadCalled;
        public TaskCompletionSource<AccountPreferences>? Pending;
        public Task<AccountPreferences> GetAsync(PreferencesScope scope, CancellationToken cancellationToken = default)
        {
            LoadCalled = true;
            return Pending?.Task ?? Task.FromResult(new AccountPreferences(WeightUnit.Kg, "Etc/UTC"));
        }
        public Task<AccountPreferences> SaveAsync(PreferencesScope scope, AccountPreferences preferences, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new HttpRequestException();
            Saved = preferences;
            return Task.FromResult(preferences);
        }
    }

    private sealed class FakeSession : IAuthSession
    {
        public SessionState State { get; private set; } = new(AuthStatus.SignedIn, new(Guid.NewGuid(), "a@example.test"));
        public TaskCompletionSource<AuthContext?>? PendingContext;
        public event EventHandler<SessionState>? StateChanged;
        public void SignOut() { State = new(AuthStatus.SignedOut); StateChanged?.Invoke(this, State); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string email, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(CancellationToken cancellationToken = default) { SignOut(); return Task.CompletedTask; }
        public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default) => PendingContext?.Task ?? Task.FromResult<AuthContext?>(new(new Uri("https://example.test/"), State.Account!.Id, "token"));
        public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(null);
    }
}
