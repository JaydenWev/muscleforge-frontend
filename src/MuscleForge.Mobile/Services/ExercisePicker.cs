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
        var root = Application.Current?.Windows.FirstOrDefault()?.Page;
        var shell = root as Shell;
        var stack = root as NavigationPage;
        var navigation = root?.Navigation;
        var accountId = session.State.Account?.Id;
        if (navigation is null || (shell is null && stack is null) || session.State.Status != AuthStatus.SignedIn) return null;
        var vm = new ExerciseSelectionViewModel(gateway, session, MainThread.BeginInvokeOnMainThread);
        var completion = new TaskCompletionSource<ExerciseSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ExercisePickerPage? page = null;
        EventHandler<ShellNavigatedEventArgs>? navigated = null;
        EventHandler<NavigationEventArgs>? popped = null;
        EventHandler<SessionState>? changed = null;
        var finished = false;
        async Task Finish(ExerciseSelection? selection)
        {
            if (finished) return;
            finished = true;
            if (shell is not null) shell.Navigated -= navigated;
            if (stack is not null) stack.Popped -= popped;
            session.StateChanged -= changed;
            vm.Dispose();
            if (page is not null && navigation.NavigationStack.LastOrDefault() == page)
                try { await navigation.PopAsync(); } catch (Exception) { }
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
        navigated = (_, _) => { if (!navigation.NavigationStack.Contains(page)) _ = Finish(null); };
        popped = (_, args) => { if (args.Page == page) _ = Finish(null); };
        changed = (_, state) => { if (state.Status != AuthStatus.SignedIn || state.Account?.Id != accountId) MainThread.BeginInvokeOnMainThread(() => _ = Finish(null)); };
        if (shell is not null) shell.Navigated += navigated;
        if (stack is not null) stack.Popped += popped;
        session.StateChanged += changed;
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(() => _ = Finish(null)));
        try { await navigation.PushAsync(page); if (finished && navigation.NavigationStack.LastOrDefault() == page) await navigation.PopAsync(); }
        catch { await Finish(null); throw; }
        return await completion.Task;
    }
}
