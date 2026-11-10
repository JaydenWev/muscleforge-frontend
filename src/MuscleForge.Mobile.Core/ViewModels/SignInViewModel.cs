using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.ViewModels;

public sealed class SignInViewModel : ObservableObject, IDisposable
{
    private readonly IAuthSession session;
    private readonly Action<Action> dispatch;
    private string email = string.Empty, password = string.Empty, message = string.Empty;
    private bool localBusy, signingOut, isCancelable, disposed;

    public SignInViewModel(IAuthSession session, Action<Action>? dispatch = null)
    {
        this.session = session;
        this.dispatch = dispatch ?? (action => action());
        SignInCommand = new AsyncRelayCommand(SignInAsync, () => CanEdit);
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanEdit);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => CanEdit);
        CancelCommand = new RelayCommand(Cancel, () => IsCancelable);
        session.StateChanged += OnStateChanged;
        ApplyState(session.State);
    }

    public string Email { get => email; set => SetProperty(ref email, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public bool IsBusy => localBusy || signingOut;
    public bool IsCancelable { get => isCancelable; private set { if (SetProperty(ref isCancelable, value)) CancelCommand.NotifyCanExecuteChanged(); } }
    private void SetLocalBusy(bool value, bool cancelable = false)
    {
        localBusy = value;
        IsCancelable = value && cancelable;
        NotifyBusyChanged();
    }
    private void NotifyBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanEdit));
        SignInCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
    }
    public bool CanEdit => !IsBusy;
    public string AccountEmail => session.State.Account?.Email ?? string.Empty;
    public IAsyncRelayCommand SignInCommand { get; }
    public IAsyncRelayCommand RetryCommand { get; }
    public IAsyncRelayCommand SignOutCommand { get; }
    public IRelayCommand CancelCommand { get; }

    private void Cancel() { SignInCommand.Cancel(); RetryCommand.Cancel(); }

    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        var trimmedEmail = Email?.Trim() ?? string.Empty;
        var secret = Password ?? string.Empty;
        try
        {
            if (trimmedEmail.Length > 254 || !MailAddress.TryCreate(trimmedEmail, out var address) || address.Address != trimmedEmail)
            { Message = "Enter a valid email address."; return; }
            if (secret.Length == 0 || secret.Length > 1024)
            { Message = "Enter your password (up to 1024 characters)."; return; }
            SetLocalBusy(true, cancelable: true);
            Message = string.Empty;
            await session.SignInAsync(trimmedEmail, secret, cancellationToken);
        }
        catch (OperationCanceledException) { Message = "Sign-in cancelled. Try again when you're ready."; }
        catch (Exception) { Message = "Could not sign in. Please try again."; }
        finally { Password = string.Empty; SetLocalBusy(false); }
    }

    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        SetLocalBusy(true, cancelable: true);
        try { await session.RestoreAsync(cancellationToken); }
        catch (OperationCanceledException) { Message = "Retry cancelled."; }
        catch (Exception) { Message = "Could not restore your session. Please try again."; }
        finally { SetLocalBusy(false); }
    }

    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        SetLocalBusy(true);
        try { await session.SignOutAsync(cancellationToken); }
        catch (OperationCanceledException) { Message = "Sign-out cancelled. Please try again."; }
        catch (Exception) { Message = "Could not sign out. Please try again."; }
        finally { SetLocalBusy(false); }
    }

    private void OnStateChanged(object? sender, SessionState state) => dispatch(() => { if (!disposed) ApplyState(state); });
    private void ApplyState(SessionState state)
    {
        signingOut = state.Status == AuthStatus.SigningOut;
        NotifyBusyChanged();
        Message = state.Message ?? (signingOut ? "Signing out…" : string.Empty);
        OnPropertyChanged(nameof(AccountEmail));
    }
    public void Dispose() { disposed = true; session.StateChanged -= OnStateChanged; }
}
