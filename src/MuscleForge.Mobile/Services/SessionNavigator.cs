using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Core.Navigation;
using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Views;

namespace MuscleForge.Mobile.Services;

public interface IWorkspacePageFactory
{
    Page CreatePage(Action showSignIn);
}

public sealed class SessionNavigator
{
    private readonly IServiceProvider services;
    private readonly IAuthSession session;
    private readonly ILocalWorkspace workspace;
    private readonly IWorkspacePageFactory workspacePages;
    private bool showSignIn;
    private bool openFullApp;
    private bool restoringWorkspace = true;
    private MuscleForge.Mobile.Core.Templates.TemplateScope? displayedScope;
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

    public SessionNavigator(IServiceProvider services, IAuthSession session, ILocalWorkspace workspace, IWorkspacePageFactory workspacePages)
    {
        this.services = services;
        this.session = session;
        this.workspace = workspace;
        this.workspacePages = workspacePages;
        session.StateChanged += OnStateChanged;
        workspace.Changed += OnWorkspaceChanged;
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
        try { await workspace.RestoreAsync(cancellationToken); }
        catch (Exception) { }
        restoringWorkspace = false;
        Show();
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

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Show();

    private void OpenSignIn()
    {
        if (session.State.Status == AuthStatus.SignedIn) openFullApp = true;
        else showSignIn = true;
        Show();
    }

    private void Show() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (window is null) return;
#if DEBUG && ANDROID
        if (weightVerificationOpen) return;
#endif
        if (restoringWorkspace) return;
        var scope = workspace.CurrentScope;
        var status = session.State.Status;
        if (scope is not null && window.Page is AppShell editingShell && editingShell.Scope == scope
            && editingShell.HasTemplateEditor && status is not AuthStatus.SigningOut and not AuthStatus.SignedIn)
            return;
        if (status == AuthStatus.SignedIn) showSignIn = false;
        if (status != AuthStatus.SignedIn) openFullApp = false;
        var local = !showSignIn && !openFullApp && scope is not null && status != AuthStatus.SigningOut
            && (status != AuthStatus.SignedIn || displayedScope == scope);
        var next = local ? SessionPage.Loading : showSignIn ? SessionPage.SignIn : SessionNavigation.Decide(status, currentPage);
        if (currentPage == next && (local ? displayedScope == scope : displayedScope is null)) return;
        currentPage = next;
        displayedScope = local ? scope : null;
        viewModel?.Dispose();
        viewModel = null;
        if (window.Page is AppShell previousShell) previousShell.Dispose();
        else if (window.Page is IDisposable disposable) disposable.Dispose();
        if (local) window.Page = workspacePages.CreatePage(OpenSignIn);
        else if (next == SessionPage.Protected)
        {
            var shell = services.GetRequiredService<AppShell>();
            shell.TemplateEditingEnded += (_, _) => Show();
            window.Page = shell;
        }
        else
        {
            viewModel = new SignInViewModel(session, MainThread.BeginInvokeOnMainThread);
            window.Page = new NavigationPage(new SignInPage(viewModel, services));
        }
    });
}
