using System.Net;
using MuscleForge.Client;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.Catalog;

public sealed class ExerciseEditorViewModel : ObservableObject, IDisposable
{
    private readonly IExerciseCatalogGateway gateway;
    private readonly IAuthSession session;
    private readonly Action<Action> dispatch;
    private readonly Guid? id;
    private CancellationTokenSource? operation;
    private long generation;
    private readonly Guid? accountId;
    private bool disposed, invalidated, isBusy, isArchived, needsLoad;
    private string name = "", equipment = "", errorMessage = "", validationMessage = "";
    private ExerciseLoadConvention loadConvention;
    private Exercise? savedExercise;
    public ExerciseEditorViewModel(IExerciseCatalogGateway gateway, IAuthSession session, Exercise? exercise = null, Action<Action>? dispatch = null)
    {
        this.gateway = gateway; this.session = session; this.dispatch = dispatch ?? (a => a()); accountId = session.State.Account?.Id; id = exercise?.Id; needsLoad = id is not null;
        if (exercise is not null) Apply(exercise);
        SaveCommand = new AsyncRelayCommand(async ct => { await SaveAsync(ct); });
        ArchiveCommand = new AsyncRelayCommand(async ct => { await ArchiveAsync(ct); });
        RetryCommand = new AsyncRelayCommand(LoadAsync);
        CancelCommand = new RelayCommand(Cancel);
        session.StateChanged += OnStateChanged;
    }
    public string Name { get => name; set => SetProperty(ref name, value); }
    public string Equipment { get => equipment; set => SetProperty(ref equipment, value); }
    public ExerciseLoadConvention LoadConvention { get => loadConvention; set => SetProperty(ref loadConvention, value); }
    public bool IsArchived { get => isArchived; private set => SetProperty(ref isArchived, value); }
    public bool NeedsLoad { get => needsLoad; private set => SetProperty(ref needsLoad, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
    public string ValidationMessage { get => validationMessage; private set => SetProperty(ref validationMessage, value); }
    public Exercise? SavedExercise { get => savedExercise; private set => SetProperty(ref savedExercise, value); }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ArchiveCommand { get; }
    public IAsyncRelayCommand RetryCommand { get; }
    public IRelayCommand CancelCommand { get; }
    private void Apply(Exercise e) { Name = e.Name; Equipment = e.Equipment; LoadConvention = e.LoadConvention; IsArchived = e.IsArchived; }
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || invalidated || id is null) return;
        Cancel(); var version = ++generation; var local = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); operation = local; IsBusy = true; ErrorMessage = "";
        try { var item = await gateway.GetAsync(id.Value, local.Token); if (!disposed && !invalidated && version == generation && !local.IsCancellationRequested) { Apply(item); NeedsLoad = false; } }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!disposed && !invalidated && version == generation && !local.IsCancellationRequested) ErrorMessage = "Could not load this exercise. Please try again."; }
        finally { if (!disposed && version == generation) { IsBusy = false; operation = null; } local.Dispose(); }
    }
    public Task<bool> SaveAsync(CancellationToken cancellationToken = default) => WriteAsync(false, cancellationToken);
    public Task<bool> ArchiveAsync(CancellationToken cancellationToken = default) => WriteAsync(true, cancellationToken);
    private async Task<bool> WriteAsync(bool archive, CancellationToken ct)
    {
        if (IsBusy || disposed || invalidated || NeedsLoad || (archive && (id is null || IsArchived))) return false;
        var trimmedName = Name?.Trim() ?? ""; var trimmedEquipment = Equipment?.Trim() ?? "";
        if (trimmedName.Length is 0 or > 100 || trimmedEquipment.Length is 0 or > 100 || !Enum.IsDefined(LoadConvention))
        { ValidationMessage = "Enter a name and equipment, each up to 100 characters, and choose a load convention."; return false; }
        ValidationMessage = ""; ErrorMessage = ""; SavedExercise = null;
        var draft = new ExerciseDraft(trimmedName, trimmedEquipment, LoadConvention, IsArchived || archive);
        var version = ++generation; var local = CancellationTokenSource.CreateLinkedTokenSource(ct); operation = local; IsBusy = true;
        try
        {
            var result = id is null ? await gateway.CreateAsync(draft, local.Token) : await gateway.UpdateAsync(id.Value, draft, local.Token);
            if (disposed || invalidated || version != generation || local.IsCancellationRequested) return false;
            SavedExercise = result; IsArchived = result.IsArchived; return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception error)
        {
            if (!disposed && !invalidated && version == generation && !local.IsCancellationRequested)
            {
                if (error is ApiException api)
                {
                    if (api.StatusCode == HttpStatusCode.BadRequest) ValidationMessage = DescribeValidation(api);
                    ErrorMessage = api.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => "Check the exercise fields and try again.",
                        HttpStatusCode.NotFound => "This exercise is no longer available. Return to the catalog.",
                        HttpStatusCode.Conflict => "This exercise changed. Return to the catalog and reload it.",
                        _ => "Could not save the exercise. Check the catalog before trying again."
                    };
                }
                else ErrorMessage = "Could not save the exercise. Check the catalog before trying again.";
            }
            return false;
        }
        finally { if (!disposed && version == generation) { IsBusy = false; operation = null; } local.Dispose(); }
    }
    public void Cancel() { generation++; operation?.Cancel(); operation = null; IsBusy = false; }
    private static string DescribeValidation(ApiException error)
    {
        // Use known field names and fixed guidance; arbitrary response text is never echoed.
        var messages = error.Problem?.Errors?.Keys.Select(field => field switch
        {
            "name" => "Name must contain 1–100 characters after trimming.",
            "equipment" => "Equipment must contain 1–100 characters after trimming.",
            "loadConvention" => "Choose total external load or weight per dumbbell.",
            "trackingType" => "Only external weight and repetitions are supported.",
            "isArchived" => "Return to the catalog and reload the exercise's archive state.",
            _ => null
        }).OfType<string>().Distinct().ToArray();
        return messages is { Length: > 0 } ? string.Join(" ", messages) : "Check name, equipment and the load convention.";
    }
    private void OnStateChanged(object? sender, SessionState state)
    {
        if (state.Status == AuthStatus.SignedIn && state.Account?.Id == accountId) return;
        invalidated = true;
        dispatch(() => { if (!disposed) { Cancel(); Name = ""; Equipment = ""; SavedExercise = null; ErrorMessage = ""; ValidationMessage = ""; } });
    }
    public void Dispose() { if (disposed) return; Cancel(); disposed = true; session.StateChanged -= OnStateChanged; Name = ""; Equipment = ""; SavedExercise = null; }
}
