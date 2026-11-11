using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile.Views.Catalog;

public sealed class ExerciseCatalogPage : ContentPage, IDisposable
{
    private readonly ExerciseListViewModel viewModel;
    private readonly ExerciseNavigation navigation;
    private bool openingEditor;

    public ExerciseCatalogPage(ExerciseListViewModel viewModel, ExerciseNavigation navigation)
    {
        this.viewModel = viewModel;
        this.navigation = navigation;
        BindingContext = viewModel;
        Title = "Exercises";
        var search = new Entry { Placeholder = "Search name or equipment", ReturnType = ReturnType.Search, AutomationId = "ExerciseSearch" };
        search.SetBinding(Entry.TextProperty, nameof(viewModel.SearchText));
        search.Completed += (_, _) => viewModel.SearchCommand.Execute(null);
        var searchButton = new Button { Text = "Search", Command = viewModel.SearchCommand, AutomationId = "ExerciseSearchButton" };
        SemanticProperties.SetDescription(search, "Search exercises by name or equipment");
        var archived = new Switch { AutomationId = "ExerciseShowArchived", MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
        SemanticProperties.SetDescription(archived, "Show archived exercises");
        archived.SetBinding(Switch.IsToggledProperty, nameof(viewModel.IncludeArchived));
        archived.Toggled += (_, _) => { if (IsLoaded) viewModel.SearchCommand.Execute(null); };
        var filter = new HorizontalStackLayout { Spacing = 12, Children = { archived, ForgeUi.Text("Show archived") } };
        var indicator = new ActivityIndicator();
        indicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsBusy));
        indicator.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var cancel = new Button { Text = "Cancel loading", Style = ForgeUi.Style("SecondaryButton"), Command = new Command(viewModel.Cancel) };
        cancel.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var error = ForgeUi.Text("");
        error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var retry = new Button { Text = "Retry", Style = ForgeUi.Style("SecondaryButton"), Command = viewModel.RetryCommand };
        retry.SetBinding(IsVisibleProperty, new Binding(nameof(viewModel.ErrorMessage), converter: new HasTextConverter()));
        var empty = ForgeUi.Text("No exercises found. Try another search or add an exercise.");
        empty.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        var items = new CollectionView { ItemsSource = viewModel.Items, ItemTemplate = new DataTemplate(() => ExerciseRow(exercise => OpenEditorAsync(exercise), new Binding(nameof(viewModel.IsBusy), source: viewModel, converter: new InverseBooleanConverter()))), SelectionMode = SelectionMode.None };
        var more = new Button { Text = "Load more", Style = ForgeUi.Style("SecondaryButton"), Command = viewModel.LoadMoreCommand };
        more.SetBinding(IsVisibleProperty, nameof(viewModel.HasMore));
        var add = ForgeUi.Button("Add exercise", () => OpenEditorAsync(null));
        add.SetBinding(IsEnabledProperty, new Binding(nameof(viewModel.IsBusy), converter: new InverseBooleanConverter()));
        add.AutomationId = "ExerciseAdd";
        items.Header = new VerticalStackLayout { Padding = new Thickness(24, 20, 24, 12), Spacing = 12, Children = { ForgeUi.Header("YOUR LIBRARY", "Exercises", "Build and manage your exercise catalog."), add, search, searchButton, filter, indicator, cancel, error, retry, empty } };
        items.Footer = new VerticalStackLayout { Padding = new Thickness(24, 12, 24, 32), Children = { more } };
        items.Margin = 0;
        Content = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star) }, Children = { items } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await viewModel.LoadAsync(); } catch (Exception) { }
    }

    protected override void OnDisappearing()
    {
        viewModel.Cancel();
        base.OnDisappearing();
    }

    private async Task OpenEditorAsync(Exercise? exercise)
    {
        if (viewModel.IsBusy || openingEditor) return;
        openingEditor = true;
        try { await navigation.OpenEditorAsync(exercise); } catch (Exception) { }
        finally { openingEditor = false; }
    }

    public void Dispose() => viewModel.Dispose();

    internal static View ExerciseRow(Func<Exercise, Task> activate, BindingBase enabledBinding)
    {
        var name = ForgeUi.Text("", "SectionTitle");
        name.SetBinding(Label.TextProperty, nameof(Exercise.Name));
        var equipment = ForgeUi.Text("", "SmallText");
        equipment.SetBinding(Label.TextProperty, nameof(Exercise.Equipment));
        var guidance = ForgeUi.Text("", "SmallText");
        guidance.SetBinding(Label.TextProperty, nameof(Exercise.LoadGuidance));
        var status = ForgeUi.Text("", "Eyebrow");
        status.SetBinding(Label.TextProperty, new Binding(nameof(Exercise.IsArchived), converter: new ArchiveStatusConverter()));
        var button = new Button { Text = "Open", Style = ForgeUi.Style("SecondaryButton"), MinimumHeightRequest = 48 };
        button.SetBinding(IsEnabledProperty, enabledBinding);
        button.Clicked += async (sender, _) =>
        {
            if (((Button)sender!).BindingContext is Exercise item)
                try { await activate(item); } catch (Exception) { }
        };
        var card = ForgeUi.Card(new VerticalStackLayout { Spacing = 8, Children = { name, equipment, guidance, status, button } });
        card.Margin = new Thickness(24, 5);
        return card;
    }
}

internal sealed class HasTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => !string.IsNullOrWhiteSpace(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
internal sealed class ArchiveStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is true ? "ARCHIVED" : "ACTIVE";
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
