using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Services;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Preferences;

namespace MuscleForge.Mobile.Views;

public partial class ConnectionPage : ContentPage
{
    private readonly IAuthSession session;
    private SignInViewModel? account;
    private AccountPreferencesViewModel? preferences;
    private readonly IAccountPreferencesGateway preferencesGateway;

    public ConnectionPage(ConnectionViewModel viewModel, AppAppearance appearance, IAuthSession session, IAccountPreferencesGateway preferencesGateway)
    {
        InitializeComponent();
        this.session = session;
        this.preferencesGateway = preferencesGateway;
        WeightUnitPicker.ItemsSource = new[] { "kg", "lb" };
        BindingContext = viewModel;
        AppearanceOptions.Initialize(appearance);
        VersionLabel.Text = "Made for the work you put in.  ·  v" + AppInfo.Current.VersionString;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var signedIn = session.State.Status == AuthStatus.SignedIn;
        AccountPanel.IsVisible = signedIn;
        PreferencesPanel.IsVisible = signedIn;
        preferences?.Dispose();
        preferences = null;
        ServerLockedMessage.IsVisible = signedIn;
        ServerPanel.IsEnabled = !signedIn;
        account?.Dispose();
        account = null;
        if (signedIn)
        {
            account = new SignInViewModel(session, MainThread.BeginInvokeOnMainThread);
            AccountPanel.BindingContext = account;
            preferences = new AccountPreferencesViewModel(preferencesGateway, session, MainThread.BeginInvokeOnMainThread);
            PreferencesPanel.BindingContext = preferences;
            _ = preferences.LoadAsync();
        }
    }

    protected override void OnDisappearing()
    {
        account?.Dispose();
        account = null;
        preferences?.Dispose();
        preferences = null;
        base.OnDisappearing();
    }
}
