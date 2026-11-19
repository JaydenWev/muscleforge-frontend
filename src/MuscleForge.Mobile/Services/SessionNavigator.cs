using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Views;

namespace MuscleForge.Mobile.Services;

public sealed class SessionNavigator(IServiceProvider services, IGuestWorkspace workspace)
{
    private Window? window;
    private CancellationTokenSource? startup;
    private bool initializing;
#if DEBUG && ANDROID
    private bool weightVerificationOpen;

    public void OpenWeightVerification()
    {
        weightVerificationOpen = true;
        startup?.Cancel();
        if (window is null) return;
        (window.Page as IDisposable)?.Dispose();
        window.Page = new NavigationPage(new DebugWeightEntryPage());
    }
#endif

    public Window CreateWindow()
    {
#if DEBUG && ANDROID
        if (weightVerificationOpen)
            return window = new Window(new NavigationPage(new DebugWeightEntryPage()));
#endif
        window = new Window(StartupPage("Opening your local library…", true));
        _ = InitializeAsync();
        return window;
    }

    private ContentPage StartupPage(string message, bool busy)
    {
        var content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 20, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = message, HorizontalTextAlignment = TextAlignment.Center, AutomationId = "LocalWorkspaceStatus" },
                new ActivityIndicator { IsRunning = busy, IsVisible = busy }
            }
        };
        var button = new Button
        {
            Text = busy ? "Cancel" : "Retry", AutomationId = busy ? "LocalWorkspaceCancel" : "LocalWorkspaceRetry"
        };
        button.Clicked += async (_, _) =>
        {
            if (busy) startup?.Cancel();
            else await InitializeAsync();
        };
        content.Children.Add(button);
        return new ContentPage { Content = content };
    }

    private async Task InitializeAsync()
    {
        if (initializing) return;
        initializing = true;
        startup = new CancellationTokenSource();
        var token = startup.Token;
        if (window is not null) window.Page = StartupPage("Opening your local library…", true);
        var cancelled = false;
        try { await workspace.InitializeAsync(token); }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception) { }
        finally
        {
            startup.Dispose();
            startup = null;
            initializing = false;
        }
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (window is null) return;
#if DEBUG && ANDROID
            if (weightVerificationOpen) return;
#endif
            if (!cancelled && workspace.IsReady)
                window.Page = services.GetRequiredService<AppShell>();
            else
                window.Page = StartupPage(cancelled ? "Opening was cancelled. Your saved data is unchanged."
                    : workspace.Error ?? "Could not open your local library. Your saved data is unchanged.", false);
        });
    }
}
