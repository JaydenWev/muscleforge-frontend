using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Services;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Views;

public partial class ConnectionPage : ContentPage
{
    private readonly IAuthSession session;
    private SignInViewModel? account;

    public ConnectionPage(ConnectionViewModel viewModel, AppAppearance appearance, IAuthSession session)
    {
        InitializeComponent();
        this.session = session;
        BindingContext = viewModel;
        AppearanceOptions.Initialize(appearance);
        VersionLabel.Text = "Made for the work you put in.  ·  v" + AppInfo.Current.VersionString;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var signedIn = session.State.Status == AuthStatus.SignedIn;
        AccountPanel.IsVisible = signedIn;
        ServerLockedMessage.IsVisible = signedIn;
        ServerPanel.IsEnabled = !signedIn;
        account?.Dispose();
        account = null;
        if (signedIn)
        {
            account = new SignInViewModel(session, MainThread.BeginInvokeOnMainThread);
            AccountPanel.BindingContext = account;
        }
    }

    protected override void OnDisappearing()
    {
        account?.Dispose();
        account = null;
        base.OnDisappearing();
    }
}
