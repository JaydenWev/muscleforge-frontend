using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Views.Catalog;

namespace MuscleForge.Mobile.Services;

public sealed class ExercisePicker(IExerciseCatalogGateway gateway, IAuthSession session) : IExercisePicker
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ExerciseSelection?> PickAsync(CancellationToken cancellationToken = default)
    {
        try { await gate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return null; }
        try { return await MainThread.InvokeOnMainThreadAsync(() => PickOnMainThreadAsync(cancellationToken)); }
        finally { gate.Release(); }
    }

    private async Task<ExerciseSelection?> PickOnMainThreadAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return null;
        var shell = Shell.Current;
        var accountId = session.State.Account?.Id;
        if (shell is null || session.State.Status != AuthStatus.SignedIn) return null;
        var vm = new ExerciseSelectionViewModel(gateway, session, MainThread.BeginInvokeOnMainThread);
        var completion = new TaskCompletionSource<ExerciseSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ExercisePickerPage? page = null;
        EventHandler<ShellNavigatedEventArgs>? navigated = null;
        EventHandler<SessionState>? changed = null;
        var finished = false;
        async Task Finish(ExerciseSelection? selection)
        {
            if (finished) return;
            finished = true;
            shell.Navigated -= navigated;
            session.StateChanged -= changed;
            vm.Dispose();
            if (page is not null && shell.Navigation.NavigationStack.LastOrDefault() == page)
                try { await shell.Navigation.PopAsync(); } catch (Exception) { }
            if (selection is not null)
            {
                try
                {
                    var context = await session.GetContextAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested || session.State.Status != AuthStatus.SignedIn || context is null || context.AccountId != selection.AccountId || context.BaseUri != selection.BaseUri) selection = null;
                }
                catch (Exception) { selection = null; }
            }
            completion.TrySetResult(selection);
        }
        page = new ExercisePickerPage(vm, Finish);
        navigated = (_, _) => { if (!shell.Navigation.NavigationStack.Contains(page)) _ = Finish(null); };
        changed = (_, state) => { if (state.Status != AuthStatus.SignedIn || state.Account?.Id != accountId) MainThread.BeginInvokeOnMainThread(() => _ = Finish(null)); };
        shell.Navigated += navigated;
        session.StateChanged += changed;
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(() => _ = Finish(null)));
        try { await shell.Navigation.PushAsync(page); if (finished && shell.Navigation.NavigationStack.LastOrDefault() == page) await shell.Navigation.PopAsync(); }
        catch { await Finish(null); throw; }
        return await completion.Task;
    }
}
