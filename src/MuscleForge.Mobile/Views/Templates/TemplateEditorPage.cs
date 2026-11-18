using System.Collections.Specialized;
using System.ComponentModel;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile.Views.Templates;

public sealed class TemplateEditorPage : ContentPage, IDisposable
{
    private readonly TemplateEditorViewModel vm;
    private readonly TemplateScope scope;
    private readonly ITemplateExercisePicker picker;
    private readonly Guid? id;
    private readonly CancellationTokenSource lifetime = new();
    private readonly VerticalStackLayout exercises = new() { Spacing = 16 };
    private readonly VerticalStackLayout editor = new() { Spacing = 14 };
    private readonly Dictionary<TemplateExerciseEditor, NotifyCollectionChangedEventHandler> setHandlers = new();
    private readonly List<(TemplateSetEditor Set, PropertyChangedEventHandler Handler)> weightHandlers = [];
    private readonly Button retry;
    private readonly Button reload;
    private readonly Button cancelLoading;
    private CancellationTokenSource? operation;
    private bool initialized, leaving, busy, confirming, disposed, loading;
    private WeightUnit unit;

    public TemplateEditorPage(TemplateEditorViewModel vm, TemplateScope scope, ITemplateExercisePicker picker, Guid? id, WeightUnit unit)
    {
        this.vm = vm;
        this.scope = scope;
        this.picker = picker;
        this.id = id;
        this.unit = unit;
        BindingContext = vm;
        Title = id.HasValue ? "Edit template" : "Create template";
        Shell.SetNavBarIsVisible(this, true);
        Shell.SetTabBarIsVisible(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(async () => await BackAsync()) });

        var name = new Entry { Placeholder = "Template name", MaxLength = 100, AutomationId = "TemplateName" };
        name.SetBinding(Entry.TextProperty, nameof(vm.Name));
        SemanticProperties.SetDescription(name, "Template name, required");
        var units = new Picker
        {
            Title = "Weight unit", ItemsSource = new[] { "kg", "lb" },
            SelectedIndex = unit == WeightUnit.Kg ? 0 : 1, MinimumHeightRequest = 48,
            AutomationId = "TemplateWeightUnit"
        };
        units.SelectedIndexChanged += (_, _) =>
        {
            var next = units.SelectedIndex == 1 ? WeightUnit.Lb : WeightUnit.Kg;
            if (vm.SetWeightUnit(next)) unit = next;
            else units.SelectedIndex = unit == WeightUnit.Kg ? 0 : 1;
        };
        var add = ForgeUi.Button("Add exercise", AddExerciseAsync, true);
        add.AutomationId = "TemplateAddExercise";
        var save = ForgeUi.Button("Save template", SaveAsync);
        save.AutomationId = "TemplateSave";
        editor.Children.Add(ForgeUi.Text("Name", "Eyebrow"));
        editor.Children.Add(name);
        editor.Children.Add(ForgeUi.Text("Weight unit", "Eyebrow"));
        editor.Children.Add(units);
        editor.Children.Add(exercises);
        editor.Children.Add(add);
        editor.Children.Add(save);

        var validation = ForgeUi.Text("");
        validation.SetBinding(Label.TextProperty, nameof(vm.ValidationMessage));
        var error = ForgeUi.Text("");
        error.SetBinding(Label.TextProperty, nameof(vm.ErrorMessage));
        var saved = ForgeUi.Text("Saved on device", "SmallText");
        saved.SetBinding(IsVisibleProperty, nameof(vm.SavedOnDevice));
        var indicator = new ActivityIndicator();
        indicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(vm.IsBusy));
        indicator.SetBinding(IsVisibleProperty, nameof(vm.IsBusy));
        retry = ForgeUi.Button("Retry loading", InitializeAsync, true);
        retry.AutomationId = "TemplateRetryLoad";
        reload = ForgeUi.Button("Reload saved version", ReloadAsync, true);
        reload.AutomationId = "TemplateReload";
        cancelLoading = ForgeUi.Button("Cancel loading", () => { operation?.Cancel(); return Task.CompletedTask; }, true);
        cancelLoading.AutomationId = "TemplateCancelLoading";
        var cancel = ForgeUi.Button("Cancel", BackAsync, true);
        cancel.AutomationId = "TemplateCancel";
        vm.Exercises.CollectionChanged += ExercisesChanged;
        vm.PropertyChanged += ViewModelChanged;
        Content = ForgeUi.PageContent(ForgeUi.Header("YOUR LIBRARY", Title, "Changes are stored on this device."),
            editor, validation, error, saved, indicator, cancelLoading, retry, reload, cancel);
        UpdateState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (initialized || busy || disposed) return;
        loading = true;
        BeginOperation();
        try
        {
            await vm.InitializeAsync(id, operation!.Token);
            if (!disposed && string.IsNullOrEmpty(vm.ErrorMessage)) initialized = true;
        }
        finally { EndOperation(); }
    }

    private async Task ReloadAsync()
    {
        if (!initialized || busy || disposed || !id.HasValue) return;
        if (vm.IsDirty)
        {
            confirming = true;
            bool confirmed;
            try { confirmed = await DisplayAlertAsync("Discard changes?", "Reloading replaces your unsaved edits with the saved version.", "Reload", "Keep editing"); }
            finally { confirming = false; }
            if (!confirmed) return;
        }
        if (disposed || busy) return;
        loading = true;
        BeginOperation();
        try { await vm.ReloadAsync(operation!.Token); }
        finally { EndOperation(); }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = BackAsync();
        return true;
    }

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (disposed || leaving || busy || vm.IsBusy || confirming) return false;
        if (!vm.IsDirty) return true;
        confirming = true;
        try { return await DisplayAlertAsync("Discard changes?", "Your unsaved template edits will be lost.", "Discard", "Keep editing"); }
        finally { confirming = false; }
    }

    private async Task BackAsync()
    {
        if (!await ConfirmLeaveAsync() || disposed) return;
        leaving = true;
        lifetime.Cancel();
        await Navigation.PopAsync();
    }

    private async Task SaveAsync()
    {
        if (!CanEdit()) return;
        BeginOperation();
        try { await vm.SaveAsync(operation!.Token); }
        finally { EndOperation(); }
    }

    private async Task AddExerciseAsync()
    {
        if (!CanEdit()) return;
        BeginOperation();
        try
        {
            var selected = await picker.PickAsync(scope, operation!.Token);
            if (selected is not null && !disposed && !operation!.IsCancellationRequested) vm.AddExercise(selected);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!disposed) await DisplayAlertAsync("Could not choose an exercise", "Check your connection or try again. Your template edits are still here.", "OK");
        }
        finally { EndOperation(); }
    }

    private bool CanEdit() => !disposed && initialized && !busy && !vm.IsBusy && !vm.IsArchived;
    private void BeginOperation()
    {
        busy = true;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        UpdateState();
    }
    private void EndOperation()
    {
        operation?.Dispose();
        operation = null;
        busy = false;
        loading = false;
        if (!disposed) UpdateState();
    }
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();
    private void UpdateState()
    {
        if (disposed) return;
        editor.IsEnabled = CanEdit();
        retry.IsVisible = id.HasValue && !initialized && !busy;
        reload.IsVisible = id.HasValue && initialized;
        reload.IsEnabled = !busy && !vm.IsBusy;
        cancelLoading.IsVisible = busy && loading;
    }

    private void ExercisesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RenderExercises();
    private void ClearRowHandlers()
    {
        foreach (var (exercise, handler) in setHandlers) exercise.Sets.CollectionChanged -= handler;
        setHandlers.Clear();
        foreach (var (set, handler) in weightHandlers) set.PropertyChanged -= handler;
        weightHandlers.Clear();
    }

    private void RenderExercises()
    {
        if (disposed) return;
        ClearRowHandlers();
        exercises.Children.Clear();
        foreach (var exercise in vm.Exercises)
        {
            var section = new VerticalStackLayout { Spacing = 10 };
            section.Children.Add(ForgeUi.Text(exercise.Metadata.Name, "SectionTitle"));
            section.Children.Add(ForgeUi.Text(exercise.Metadata.IsArchived ? "Archived exercise — retained in this template" : exercise.Metadata.Equipment, "SmallText"));
            section.Children.Add(Buttons(
                ("Move exercise up", "TemplateExerciseUp", () => vm.MoveExercise(exercise, -1)),
                ("Move exercise down", "TemplateExerciseDown", () => vm.MoveExercise(exercise, 1)),
                ("Remove exercise", "TemplateExerciseRemove", () => vm.RemoveExercise(exercise))));
            var rest = new Entry { Placeholder = "Optional rest in seconds", Keyboard = Keyboard.Numeric, AutomationId = "TemplateRestSeconds" };
            rest.SetBinding(Entry.TextProperty, new Binding(nameof(exercise.RestSecondsText), source: exercise));
            SemanticProperties.SetDescription(rest, "Rest between sets in seconds");
            section.Children.Add(rest);
            NotifyCollectionChangedEventHandler handler = (_, _) => RenderExercises();
            setHandlers.Add(exercise, handler);
            exercise.Sets.CollectionChanged += handler;
            foreach (var set in exercise.Sets) section.Children.Add(SetView(exercise, set));
            var addSet = ForgeUi.Button("Add set", () => { exercise.AddSet(); return Task.CompletedTask; }, true);
            addSet.AutomationId = "TemplateAddSet";
            section.Children.Add(addSet);
            exercises.Children.Add(ForgeUi.Card(section));
        }
    }

    private View SetView(TemplateExerciseEditor exercise, TemplateSetEditor set)
    {
        var kind = new Picker
        {
            Title = "Set kind", ItemsSource = new[] { "Warm-up", "Working" },
            SelectedIndex = set.Kind == TemplateSetKind.Warmup ? 0 : 1,
            MinimumHeightRequest = 48, AutomationId = "TemplateSetKind"
        };
        kind.SelectedIndexChanged += (_, _) => set.Kind = kind.SelectedIndex == 0 ? TemplateSetKind.Warmup : TemplateSetKind.Working;
        Entry Number(string placeholder, string property, string automation)
        {
            var input = new Entry { Placeholder = placeholder, Keyboard = Keyboard.Numeric, AutomationId = automation };
            input.SetBinding(Entry.TextProperty, new Binding(property, source: set));
            SemanticProperties.SetDescription(input, placeholder);
            return input;
        }
        var weight = new WeightEntry { Unit = unit, AutomationId = "TemplateSetWeight", Text = set.WeightText };
        weight.TextChanged += (_, _) => set.WeightText = weight.Text ?? "";
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(set.WeightText) && weight.Text != set.WeightText) weight.Text = set.WeightText;
        };
        set.PropertyChanged += handler;
        weightHandlers.Add((set, handler));
        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                ForgeUi.Text("Set", "Eyebrow"), kind,
                Number("Minimum reps", nameof(set.RepMinText), "TemplateSetRepMin"),
                Number("Maximum reps", nameof(set.RepMaxText), "TemplateSetRepMax"),
                ForgeUi.Text("Target weight (optional)", "SmallText"), weight,
                Buttons(("Move set up", "TemplateSetUp", () => exercise.MoveSet(set, -1)),
                    ("Move set down", "TemplateSetDown", () => exercise.MoveSet(set, 1)),
                    ("Remove set", "TemplateSetRemove", () => exercise.RemoveSet(set)))
            }
        };
    }

    private static VerticalStackLayout Buttons(params (string text, string id, Action action)[] actions)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        foreach (var (text, id, action) in actions)
        {
            var button = ForgeUi.Button(text, () => { action(); return Task.CompletedTask; }, true);
            button.AutomationId = id;
            button.MinimumHeightRequest = 48;
            stack.Children.Add(button);
        }
        return stack;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        vm.Exercises.CollectionChanged -= ExercisesChanged;
        vm.PropertyChanged -= ViewModelChanged;
        ClearRowHandlers();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
