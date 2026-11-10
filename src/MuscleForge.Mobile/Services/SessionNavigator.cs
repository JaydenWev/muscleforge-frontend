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

    public SessionNavigator(IServiceProvider services, IAuthSession session)
    {
        this.services = services;
        this.session = session;
        session.StateChanged += OnStateChanged;
    }

    public Window CreateWindow()
    {
        var loading = new ContentPage { Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center, Padding = 24, Spacing = 16,
            Children = { new ActivityIndicator { IsRunning = true }, new Label { Text = "Restoring your session…", HorizontalTextAlignment = TextAlignment.Center } }
        } };
        window = new Window(loading);
        _ = RestoreAsync();
        return window;
    }

    private async Task RestoreAsync()
    {
        try { await session.RestoreAsync(); }
        catch (Exception) { }
        Show();
    }

    private void OnStateChanged(object? sender, SessionState state)
    {
        Show();
    }

    private void Show() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (window is null) return;
        var next = SessionNavigation.Decide(session.State.Status, currentPage);
        if (currentPage == next) return;
        currentPage = next;
        viewModel?.Dispose();
        viewModel = null;
        if (next == SessionPage.Protected) window.Page = services.GetRequiredService<AppShell>();
        else
        {
            viewModel = new SignInViewModel(session, MainThread.BeginInvokeOnMainThread);
            window.Page = new NavigationPage(new SignInPage(viewModel, services));
        }
    });
}
