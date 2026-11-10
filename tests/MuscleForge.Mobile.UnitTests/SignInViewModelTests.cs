using Xunit;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.ViewModels;

namespace MuscleForge.Mobile.UnitTests;

public class SignInViewModelTests
{
    [Fact]
    public async Task SignIn_trims_email_preserves_password_and_clears_it()
    {
        var session = new FakeSession();
        using var vm = new SignInViewModel(session) { Email = "  user@example.com  ", Password = " spaced password " };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Equal("user@example.com", session.Email);
        Assert.Equal(" spaced password ", session.Password);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Theory]
    [InlineData("bad", "password")]
    [InlineData("user@example.com", "")]
    public async Task Invalid_input_is_reported_and_password_cleared(string email, string password)
    {
        var session = new FakeSession();
        using var vm = new SignInViewModel(session) { Email = email, Password = password };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.Null(session.Email);
        Assert.NotEmpty(vm.Message);
        Assert.Empty(vm.Password);
    }

    [Fact]
    public async Task Session_failure_is_visible_and_password_cleared()
    {
        var session = new FakeSession { Throw = true };
        using var vm = new SignInViewModel(session) { Email = "user@example.com", Password = "secret" };
        await vm.SignInCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.Message);
        Assert.Empty(vm.Password);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Pending_sign_in_shows_busy_and_cancellation_clears_password()
    {
        var session = new FakeSession { WaitForCancellation = true };
        using var vm = new SignInViewModel(session) { Email = "user@example.com", Password = "secret" };
        var operation = vm.SignInCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanEdit);
        Assert.True(vm.IsCancelable);
        Assert.False(vm.RetryCommand.CanExecute(null));
        Assert.False(vm.SignOutCommand.CanExecute(null));
        vm.CancelCommand.Execute(null);
        await operation;
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Password);
        Assert.Contains("cancelled", vm.Message);
    }

    [Fact]
    public void Disposed_view_model_ignores_queued_state_updates()
    {
        var session = new FakeSession();
        var callbacks = new Queue<Action>();
        var vm = new SignInViewModel(session, callbacks.Enqueue);
        session.Publish(new(AuthStatus.StorageError, null, "Storage unavailable"));
        vm.Dispose();
        callbacks.Dequeue()();
        Assert.Empty(vm.Message);
    }

    [Fact]
    public async Task Logout_calls_session_and_surfaces_storage_error()
    {
        var session = new FakeSession();
        using var vm = new SignInViewModel(session);
        await vm.SignOutCommand.ExecuteAsync(null);
        Assert.True(session.SignedOut);
        Assert.Equal("Storage unavailable", vm.Message);
    }

    private sealed class FakeSession : IAuthSession
    {
        public SessionState State { get; private set; } = new(AuthStatus.SignedOut);
        public event EventHandler<SessionState>? StateChanged;
        public string? Email, Password;
        public bool Throw, SignedOut, WaitForCancellation;
        public async Task SignInAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            Email = email; Password = password;
            if (Throw) throw new InvalidOperationException();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public void Publish(SessionState state) { State = state; StateChanged?.Invoke(this, state); }
        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            SignedOut = true; Publish(new(AuthStatus.StorageError, null, "Storage unavailable"));
            return Task.CompletedTask;
        }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(null);
        public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(null);
    }
}
