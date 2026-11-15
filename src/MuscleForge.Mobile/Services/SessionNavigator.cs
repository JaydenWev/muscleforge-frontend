using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Core.Navigation;
using MuscleForge.Mobile.Views;

namespace MuscleForge.Mobile.Services;

public sealed class SessionNavigator
{
    private readonly IServiceProvider services;
    private readonly IAuthSession session;
    private Window? window;
    private SignInViewModel? viewModel;
    private SessionPage currentPage = SessionPage.Loading;
    private CancellationTokenSource? restorationCancellation;
#if DEBUG && ANDROID
    private bool weightVerificationOpen;

    public void OpenWeightVerification()
    {
        weightVerificationOpen = true;
        if (window is null) return;
        viewModel?.Dispose();
        viewModel = null;
        if (window.Page is AppShell previousShell) previousShell.Dispose();
        window.Page = new NavigationPage(new DebugWeightEntryPage());
    }
#endif

    public SessionNavigator(IServiceProvider services, IAuthSession session)
    {
        this.services = services;
        this.session = session;
        session.StateChanged += OnStateChanged;
    }

    public Window CreateWindow()
    {
#if DEBUG && ANDROID
        if (weightVerificationOpen)
        {
            window = new Window(new NavigationPage(new DebugWeightEntryPage()));
            return window;
        }
#endif
        restorationCancellation = new CancellationTokenSource();
        var loading = new ContentPage { Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center, Padding = 24, Spacing = 16,
            Children =
            {
                new ActivityIndicator { IsRunning = true },
                new Label { Text = "Restoring your session…", HorizontalTextAlignment = TextAlignment.Center },
                new Button
                {
                    Text = "Cancel", AutomationId = "RestoreSessionCancelButton",
                    Command = new Command(() => restorationCancellation?.Cancel())
                }
            }
        } };
        window = new Window(loading);
        _ = RestoreAsync(restorationCancellation.Token);
        return window;
    }

    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        try { await session.RestoreAsync(cancellationToken); }
        catch (Exception) { }
        finally
        {
            restorationCancellation?.Dispose();
            restorationCancellation = null;
        }
        Show();
    }

    private void OnStateChanged(object? sender, SessionState state)
    {
        Show();
    }

    private void Show() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (window is null) return;
#if DEBUG && ANDROID
        if (weightVerificationOpen) return;
#endif
        var next = SessionNavigation.Decide(session.State.Status, currentPage);
        if (currentPage == next) return;
        currentPage = next;
        viewModel?.Dispose();
        viewModel = null;
        if (window.Page is AppShell previousShell) previousShell.Dispose();
        if (next == SessionPage.Protected) window.Page = services.GetRequiredService<AppShell>();
        else
        {
            viewModel = new SignInViewModel(session, MainThread.BeginInvokeOnMainThread);
            window.Page = new NavigationPage(new SignInPage(viewModel, services));
        }
    });
}
