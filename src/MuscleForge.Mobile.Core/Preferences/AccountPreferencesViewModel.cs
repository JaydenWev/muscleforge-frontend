using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Core.Preferences;

public sealed class AccountPreferencesViewModel : ObservableObject, IDisposable
{
    private readonly IAccountPreferencesGateway gateway;
    private readonly IAuthSession session;
    private readonly Action<Action> dispatch;
    private readonly Guid accountId;
    private readonly CancellationTokenSource lifetime = new();
    private string unit = "kg", timeZoneId = "", message = "Loading preferences…";
    private bool busy, loaded;
    private bool disposed;
    private int invalidated;
    private PreferencesScope? scope;

    public AccountPreferencesViewModel(IAccountPreferencesGateway gateway, IAuthSession session, Action<Action> dispatch)
    {
        this.gateway = gateway;
        this.session = session;
        this.dispatch = dispatch;
        accountId = session.State.Account?.Id ?? Guid.Empty;
        session.StateChanged += SessionChanged;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        UsePhoneTimeZoneCommand = new RelayCommand(() => TimeZoneId = TimeZoneInfo.Local.Id);
    }

    public string Unit { get => unit; set => SetProperty(ref unit, value); }
    public string TimeZoneId { get => timeZoneId; set => SetProperty(ref timeZoneId, value); }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public bool Busy { get => busy; private set { if (SetProperty(ref busy, value)) OnPropertyChanged(nameof(CanEdit)); } }
    public bool Loaded { get => loaded; private set { if (SetProperty(ref loaded, value)) OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => Loaded && !Busy && !disposed && Volatile.Read(ref invalidated) == 0;
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand UsePhoneTimeZoneCommand { get; }

    public async Task LoadAsync()
    {
        if (disposed || Busy) return;
        Busy = true;
        Message = "Loading preferences…";
        try
        {
            var context = await session.GetContextAsync(lifetime.Token);
            if (!Current()) return;
            if (context is null || context.AccountId != accountId) throw new InvalidOperationException("Session changed.");
            var requestScope = new PreferencesScope(context.BaseUri, context.AccountId);
            var result = await gateway.GetAsync(requestScope, lifetime.Token);
            if (!Current()) return;
            Apply(result);
            scope = requestScope;
            Loaded = true;
            Message = "Preferences loaded.";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { if (Current()) Message = "Could not load preferences. Check your connection and try again."; }
        finally { if (!disposed) Busy = false; }
    }

    private async Task SaveAsync()
    {
        if (!CanEdit || scope is null || !Current()) return;
        if ((Unit != "kg" && Unit != "lb") || string.IsNullOrWhiteSpace(TimeZoneId))
        {
            Message = "Choose a unit and enter an IANA time zone, such as Europe/Amsterdam.";
            return;
        }
        Busy = true;
        Message = "Saving preferences…";
        try
        {
            var result = await gateway.SaveAsync(scope, new(Unit == "kg" ? WeightUnit.Kg : WeightUnit.Lb, TimeZoneId), lifetime.Token);
            if (!Current()) return;
            Apply(result);
            Message = "Preferences saved.";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception) { if (Current()) Message = "Could not save preferences. Check the time zone and connection, then try again."; }
        finally { if (!disposed) Busy = false; }
    }

    private void Apply(AccountPreferences preferences)
    {
        Unit = preferences.WeightUnit == WeightUnit.Kg ? "kg" : "lb";
        TimeZoneId = preferences.TimeZoneId;
    }
    private bool Current() => !disposed && Volatile.Read(ref invalidated) == 0 && session.State.Status == AuthStatus.SignedIn && session.State.Account?.Id == accountId;
    private void SessionChanged(object? sender, SessionState state)
    {
        if (state.Status != AuthStatus.SignedIn || state.Account?.Id != accountId)
        {
            Interlocked.Exchange(ref invalidated, 1);
            lifetime.Cancel();
            dispatch(Dispose);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        scope = null;
        session.StateChanged -= SessionChanged;
        Unit = "kg";
        TimeZoneId = "";
        Loaded = false;
        Message = "";
        OnPropertyChanged(nameof(CanEdit));
    }
}
