using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Catalog;

namespace MuscleForge.Mobile.Views.Catalog;

public sealed class ExerciseEditorPage : ContentPage
{
    private readonly ExerciseEditorViewModel viewModel;
    private readonly bool editing;
    private bool started;

    public ExerciseEditorPage(ExerciseEditorViewModel viewModel, bool editing)
    {
        this.viewModel = viewModel;
        this.editing = editing;
        BindingContext = viewModel;
        Title = editing ? "Edit exercise" : "Add exercise";
        Shell.SetNavBarIsVisible(this, true);
        var name = new Entry { Placeholder = "Exercise name", MaxLength = 100, AutomationId = "ExerciseName" };
        name.SetBinding(Entry.TextProperty, nameof(viewModel.Name));
        SemanticProperties.SetDescription(name, "Exercise name, required");
        var equipment = new Entry { Placeholder = "Equipment", MaxLength = 100, AutomationId = "ExerciseEquipment" };
        equipment.SetBinding(Entry.TextProperty, nameof(viewModel.Equipment));
        SemanticProperties.SetDescription(equipment, "Equipment, required");
        var convention = new Picker { Title = "Choose load convention", MinimumHeightRequest = 48, AutomationId = "ExerciseLoadConvention", ItemsSource = new[] { "Total external load", "Per dumbbell" } };
        convention.SetBinding(Picker.SelectedIndexProperty, new Binding(nameof(viewModel.LoadConvention), converter: new ConventionIndexConverter()));
        SemanticProperties.SetDescription(convention, "Load convention");
        foreach (var input in new VisualElement[] { name, equipment, convention })
            input.SetBinding(IsEnabledProperty, ReadyBinding());
        var guidance = ForgeUi.Text("Total load: record the combined external load. Per dumbbell: record the weight of one dumbbell.", "SmallText");
        var status = ForgeUi.Text("", "Eyebrow");
        status.SetBinding(Label.TextProperty, new Binding(nameof(viewModel.IsArchived), converter: new ArchiveStatusConverter()));
        var validation = ForgeUi.Text(""); validation.SetBinding(Label.TextProperty, nameof(viewModel.ValidationMessage));
        var error = ForgeUi.Text(""); error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var busy = new ActivityIndicator(); busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsBusy)); busy.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var cancel = new Button { Text = "Cancel operation", Style = ForgeUi.Style("SecondaryButton"), Command = viewModel.CancelCommand };
        cancel.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var retry = new Button { Text = "Retry loading", Style = ForgeUi.Style("SecondaryButton"), Command = viewModel.RetryCommand };
        retry.SetBinding(IsVisibleProperty, new MultiBinding { Converter = new EditorRetryConverter(), Bindings = { new Binding(nameof(viewModel.IsBusy)), new Binding(nameof(viewModel.NeedsLoad)) } });
        var save = ForgeUi.Button("Save exercise", SaveAsync); save.AutomationId = "ExerciseSave";
        save.SetBinding(IsEnabledProperty, ReadyBinding());
        var archive = ForgeUi.Button("Archive exercise", ArchiveAsync, secondary: true); archive.AutomationId = "ExerciseArchive";
        archive.IsVisible = editing;
        archive.SetBinding(IsEnabledProperty, ReadyBinding());
        if (editing) archive.SetBinding(IsVisibleProperty, new Binding(nameof(viewModel.IsArchived), converter: new InverseBooleanConverter()));
        Content = ForgeUi.PageContent(ForgeUi.Header("YOUR LIBRARY", Title, "Name and equipment are required."), status,
            new VerticalStackLayout { Spacing = 12, Children = { ForgeUi.Text("Name", "Eyebrow"), name, ForgeUi.Text("Equipment", "Eyebrow"), equipment, ForgeUi.Text("Load convention", "Eyebrow"), convention, guidance, validation, error, busy, cancel, retry, save, archive } });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (started || !editing) return;
        started = true;
        try { await viewModel.LoadAsync(); } catch (Exception) { }
    }

    private static MultiBinding ReadyBinding() => new()
    {
        Converter = new EditorReadyConverter(),
        Bindings = { new Binding(nameof(ExerciseEditorViewModel.IsBusy)), new Binding(nameof(ExerciseEditorViewModel.NeedsLoad)) }
    };

    private async Task SaveAsync()
    {
        try { if (await viewModel.SaveAsync()) await Navigation.PopAsync(); } catch (Exception) { }
    }

    private async Task ArchiveAsync()
    {
        try
        {
            if (viewModel.IsBusy || viewModel.IsArchived) return;
            if (await DisplayAlertAsync("Archive exercise?", "This permanently archives the exercise and hides it from normal exercise selection. This cannot be undone.", "Archive", "Cancel") && await viewModel.ArchiveAsync())
                await Navigation.PopAsync();
        }
        catch (Exception) { }
    }
}

internal sealed class ConventionIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is ExerciseLoadConvention.PerDumbbell ? 1 : 0;
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is 1 ? ExerciseLoadConvention.PerDumbbell : ExerciseLoadConvention.TotalLoad;
}

internal sealed class EditorRetryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) => values.Length == 2 && values[0] is false && values[1] is true;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class EditorReadyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) => values.Length == 2 && values[0] is false && values[1] is false;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
