using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Views.Catalog;

namespace MuscleForge.Mobile.Services;

public sealed class ExerciseNavigation(IExerciseCatalogGateway gateway, IAuthSession session)
{
    public async Task OpenEditorAsync(Exercise? exercise)
    {
        var shell = Shell.Current;
        var accountId = session.State.Account?.Id;
        var vm = new ExerciseEditorViewModel(gateway, session, exercise, MainThread.BeginInvokeOnMainThread);
        var page = new ExerciseEditorPage(vm, exercise is not null);
        EventHandler<ShellNavigatedEventArgs>? navigated = null;
        EventHandler<SessionState>? changed = null;
        var disposed = false;
        void Cleanup()
        {
            if (disposed) return;
            disposed = true;
            shell.Navigated -= navigated;
            session.StateChanged -= changed;
            vm.Dispose();
        }
        navigated = (_, _) =>
        {
            if (shell.Navigation.NavigationStack.Contains(page)) return;
            Cleanup();
        };
        changed = (_, state) => { if (state.Status != AuthStatus.SignedIn || state.Account?.Id != accountId) MainThread.BeginInvokeOnMainThread(Cleanup); };
        shell.Navigated += navigated;
        session.StateChanged += changed;
        try { await shell.Navigation.PushAsync(page); }
        catch { Cleanup(); throw; }
    }
}
