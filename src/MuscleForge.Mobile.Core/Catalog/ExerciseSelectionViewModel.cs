using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.Catalog;

public sealed class ExerciseSelectionViewModel : ObservableObject, IDisposable
{
    private readonly IExerciseCatalogGateway gateway;
    private readonly IAuthSession session;
    private readonly Action<Action> dispatch;
    private CancellationTokenSource? operation;
    private long generation;
    private readonly Guid? accountId;
    private bool disposed, invalidated, isBusy;
    private string errorMessage = "";
    public ExerciseSelectionViewModel(IExerciseCatalogGateway gateway, IAuthSession session, Action<Action>? dispatch = null)
    {
        this.gateway = gateway; this.session = session; this.dispatch = dispatch ?? (a => a()); accountId = session.State.Account?.Id;
        List = new(gateway, session, dispatch); CancelCommand = new RelayCommand(Cancel); session.StateChanged += OnStateChanged;
    }
    public ExerciseListViewModel List { get; }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
    public IRelayCommand CancelCommand { get; }
    public async Task<ExerciseSelection?> SelectAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        if (disposed || invalidated || IsBusy) return null;
        if (exercise.IsArchived) { ErrorMessage = "Archived exercises cannot be selected."; return null; }
        var version = ++generation; var local = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); operation = local; IsBusy = true; ErrorMessage = "";
        try
        {
            var result = await gateway.SelectAsync(exercise.Id, local.Token);
            if (disposed || invalidated || version != generation || local.IsCancellationRequested) return null;
            if (result is null) ErrorMessage = "This exercise is archived and cannot be selected.";
            return result;
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception) { if (!disposed && !invalidated && version == generation && !local.IsCancellationRequested) ErrorMessage = "Could not select this exercise. Please try again."; return null; }
        finally { if (!disposed && version == generation) { IsBusy = false; operation = null; } local.Dispose(); }
    }
    public void Cancel() { generation++; operation?.Cancel(); operation = null; IsBusy = false; List.Cancel(); }
    private void OnStateChanged(object? sender, SessionState state)
    {
        if (state.Status == AuthStatus.SignedIn && state.Account?.Id == accountId) return;
        invalidated = true;
        dispatch(() => { if (!disposed) { Cancel(); ErrorMessage = ""; } });
    }
    public void Dispose() { if (disposed) return; Cancel(); disposed = true; session.StateChanged -= OnStateChanged; List.Dispose(); ErrorMessage = ""; }
}
