using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Core.Templates;

public sealed class TemplateEditorViewModel : ObservableObject
{
    private readonly ILocalTemplateStore store;
    private readonly TemplateScope scope;
    private readonly CultureInfo culture;
    private readonly Action<Action> dispatch;
    private LocalTemplate? saved;
    private Guid id = Guid.NewGuid();
    private string name = "", error = "", validation = "";
    private bool busy, dirty, savedOnDevice, initialized;
    private WeightUnit unit;
    private long editVersion;
    public TemplateEditorViewModel(ILocalTemplateStore store, TemplateScope scope, WeightUnit unit, CultureInfo culture, Action<Action>? dispatch = null)
    {
        this.store = store;
        this.scope = scope;
        this.unit = unit;
        this.culture = culture;
        this.dispatch = dispatch ?? (a => a());
        SaveCommand = new AsyncRelayCommand(async ct => { await SaveAsync(ct); });
        ArchiveCommand = new AsyncRelayCommand(async ct => { await ArchiveAsync(ct); });
    }
    public ObservableCollection<TemplateExerciseEditor> Exercises { get; } = [];
    public string Name { get => name; set { if (SetProperty(ref name, value)) Changed(); } }
    public bool IsBusy { get => busy; private set => SetProperty(ref busy, value); }
    public bool IsDirty { get => dirty; private set => SetProperty(ref dirty, value); }
    public bool SavedOnDevice { get => savedOnDevice; private set => SetProperty(ref savedOnDevice, value); }
    public bool IsArchived => saved?.IsArchived ?? false;
    public string ErrorMessage { get => error; private set => SetProperty(ref error, value); }
    public string ValidationMessage { get => validation; private set => SetProperty(ref validation, value); }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ArchiveCommand { get; }
    private void Changed()
    {
        editVersion++;
        IsDirty = true;
        SavedOnDevice = false;
    }
    public async Task InitializeAsync(Guid? templateId = null, CancellationToken ct = default)
    {
        if (initialized || IsBusy) return;
        if (templateId is null)
        {
            initialized = true;
            return;
        }
        await LoadAsync(templateId.Value, ct);
    }

    // The caller confirms that existing edits may be discarded before invoking this.
    public Task<bool> ReloadAsync(CancellationToken ct = default) =>
    saved is null || IsBusy ? Task.FromResult(false) : LoadAsync(saved.Id, ct);

    private async Task<bool> LoadAsync(Guid templateId, CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = "";
        var startingVersion = editVersion;
        var applied = false;
        try
        {
            var item = await store.GetAsync(scope, templateId, ct);
            ct.ThrowIfCancellationRequested();
            await DispatchAsync(() =>
            {
                if (ct.IsCancellationRequested)
                    ErrorMessage = "Loading cancelled. Your edits are still here.";
                else if (item is null)
                ErrorMessage = "This template is no longer available. Your edits are still here.";
                else if (startingVersion != editVersion)
                ErrorMessage = "The template changed while loading. Your edits are still here.";
                else
                {
                    Apply(item);
                    ValidationMessage = "";
                    initialized = true;
                    applied = true;
                }
            });
        }
        catch (OperationCanceledException)
        {
            await DispatchAsync(() => ErrorMessage = "Loading cancelled. Your edits are still here.");
        }
        catch (Exception)
        {
            await DispatchAsync(() => ErrorMessage = "Could not load this template. Your edits are still here; try again.");
        }
        finally
        {
            await DispatchAsync(() => IsBusy = false);
        }
        return applied;
    }
    private void Apply(LocalTemplate item)
    {
        saved = item;
        id = item.Id;
        Name = item.Name;
        Exercises.Clear();
        foreach (var exercise in item.Exercises) Exercises.Add(new(exercise, unit, culture, Changed));
        IsDirty = false;
        SavedOnDevice = true;
        OnPropertyChanged(nameof(IsArchived));
    }
    public bool AddExercise(LocalExerciseMetadata metadata)
    {
        if (IsArchived || IsBusy || metadata.Scope != scope || metadata.IsArchived || Exercises.Count >= 100)
        {
            ValidationMessage = "Choose an available exercise; a template can contain up to 100 exercises.";
            return false;
        }
        Exercises.Add(new(new LocalTemplateExercise(Guid.NewGuid(), Exercises.Count, metadata, null, []), unit, culture, Changed));
        Changed();
        ValidationMessage = "";
        return true;
    }
    public void RemoveExercise(TemplateExerciseEditor exercise)
    {
        if (!IsBusy && !IsArchived && Exercises.Remove(exercise)) Changed();
    }
    public void MoveExercise(TemplateExerciseEditor exercise, int direction)
    {
        var from = Exercises.IndexOf(exercise);
        var to = from + direction;
        if (!IsBusy && !IsArchived && from >= 0 && Math.Abs(direction) == 1 && to >= 0 && to < Exercises.Count)
        {
            Exercises.Move(from, to);
            Changed();
        }
    }
    public bool SetWeightUnit(WeightUnit value)
    {
        if (!Enum.IsDefined(value)) return false;
        foreach (var exercise in Exercises) foreach (var set in exercise.Sets) if (!set.CanChangeUnit())
        {
            ValidationMessage = "Correct the weight before changing units.";
            return false;
        }
        foreach (var exercise in Exercises)
        {
            foreach (var set in exercise.Sets) set.ChangeUnit(value);
            exercise.ChangeUnit(value);
        }
        unit = value;
        ValidationMessage = "";
        return true;
    }
    public LocalTemplate CreateSnapshot()
    {
        var exercises = Exercises.Select((e, i) => e.Snapshot(i)).ToArray();
        var candidate = new LocalTemplate(scope, id, Name, saved?.Revision ?? 0, saved?.ArchivedAt, exercises);
        LocalTemplateValidation.ValidateForSave(scope, candidate, saved);
        return candidate;
    }
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (!initialized || IsBusy || IsArchived) return false;
        LocalTemplate candidate;
        try
        {
            candidate = CreateSnapshot();
        }
        catch (TemplateValidationException e)
        {
            ValidationMessage = e.Message;
            return false;
        }
        IsBusy = true;
        ErrorMessage = "";
        ValidationMessage = "";
        var committedVersion = editVersion;
        try
        {
            var result = await store.SaveAsync(scope, candidate, candidate.Revision, ct);
            await DispatchAsync(() => { saved = result; IsDirty = editVersion != committedVersion; SavedOnDevice = !IsDirty; });
            return true;
        }
        catch (OperationCanceledException)
        {
            await DispatchAsync(() => ErrorMessage = "Save cancelled. Your edits are still here.");
        }
        catch (TemplateConflictException)
        {
            await DispatchAsync(() => ErrorMessage = "This template changed elsewhere. Your edits are still here; reload before saving again.");
        }
        catch (TemplateValidationException)
        {
            await DispatchAsync(() => ValidationMessage = "Check the template fields and try again.");
        }
        catch (Exception)
        {
            await DispatchAsync(() => ErrorMessage = "Could not save this template. Your edits are still here; try again.");
        }
        finally
        {
            await DispatchAsync(() => IsBusy = false);
        }
        return false;
    }
    public async Task<bool> ArchiveAsync(CancellationToken ct = default)
    {
        if (!initialized || IsBusy || saved is null || IsArchived) return false;
        if (IsDirty)
        {
            ErrorMessage = "Save your edits before archiving.";
            return false;
        }
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var result = await store.ArchiveAsync(scope, id, saved.Revision, ct);
            await DispatchAsync(() => { saved = result; SavedOnDevice = true; OnPropertyChanged(nameof(IsArchived)); });
            return true;
        }
        catch (OperationCanceledException)
        {
            await DispatchAsync(() => ErrorMessage = "Archive cancelled. Try again when ready.");
        }
        catch (TemplateConflictException)
        {
            await DispatchAsync(() => ErrorMessage = "This template changed elsewhere. Reload before archiving.");
        }
        catch (Exception)
        {
            await DispatchAsync(() => ErrorMessage = "Could not archive this template. Please try again.");
        }
        finally
        {
            await DispatchAsync(() => IsBusy = false);
        }
        return false;
    }
    private Task DispatchAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatch(() => { try { action(); completion.SetResult(); } catch (Exception error) { completion.SetException(error); } });
        return completion.Task;
    }

}
