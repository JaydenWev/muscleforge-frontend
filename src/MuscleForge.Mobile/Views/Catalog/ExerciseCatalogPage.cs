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
        var searchButton = new Button { Text = "Search", Command = viewModel.SearchCommand, AutomationId = "ExerciseSearchButton", Style = ForgeUi.Style("SecondaryButton"), MinimumHeightRequest = 48, Padding = new Thickness(14, 8) };
        SemanticProperties.SetDescription(search, "Search exercises by name or equipment");
        var archived = new Switch { AutomationId = "ExerciseShowArchived", MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
        SemanticProperties.SetDescription(archived, "Show archived exercises");
        archived.SetBinding(Switch.IsToggledProperty, nameof(viewModel.IncludeArchived));
        archived.Toggled += (_, _) => { if (IsLoaded) viewModel.SearchCommand.Execute(null); };
        var filter = new HorizontalStackLayout { Spacing = 8, Children = { archived, ForgeUi.Text("Show archived", "SmallText") } };
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
        var add = ForgeUi.Button("+ Add", () => OpenEditorAsync(null));
        add.MinimumHeightRequest = 44;
        add.Padding = new Thickness(16, 8);
        add.SetBinding(IsEnabledProperty, new Binding(nameof(viewModel.IsBusy), converter: new InverseBooleanConverter()));
        add.AutomationId = "ExerciseAdd";
        var heading = ForgeUi.Text("Exercises", "DisplayTitle");
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var titleBar = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
        titleBar.Add(heading);
        titleBar.Add(add, 1);
        var searchBar = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        searchBar.Add(search);
        searchBar.Add(searchButton, 1);
        var searchFrame = new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(12, 2), Content = searchBar };
        searchFrame.SetDynamicResource(BackgroundColorProperty, "SurfaceRaised");
        items.Header = new VerticalStackLayout { Padding = new Thickness(24, 20, 24, 12), Spacing = 14, Children = { titleBar, searchFrame, filter, indicator, cancel, error, retry, empty } };
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
        name.FontSize = 15;
        name.LineBreakMode = LineBreakMode.TailTruncation;
        name.SetBinding(Label.TextProperty, nameof(Exercise.Name));
        var equipment = ForgeUi.Text("", "SmallText");
        equipment.SetBinding(Label.TextProperty, nameof(Exercise.Equipment));
        var status = ForgeUi.Text("ARCHIVED", "Eyebrow");
        status.SetBinding(IsVisibleProperty, nameof(Exercise.IsArchived));
        var button = new Button { Text = "", BackgroundColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 0, MinimumHeightRequest = 64, Padding = 0 };
        button.SetBinding(SemanticProperties.DescriptionProperty, nameof(Exercise.Name));
        button.SetBinding(IsEnabledProperty, enabledBinding);
        button.Clicked += async (sender, _) =>
        {
            if (((Button)sender!).BindingContext is Exercise item)
                try { await activate(item); } catch (Exception) { }
        };
        var icon = ForgeUi.IconTile(ForgeIcon.Dumbbell, 44);
        var details = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Children = { name, equipment, status } };
        var arrow = ForgeUi.Icon(ForgeIcon.ArrowRight, 18, "TextMuted");
        arrow.VerticalOptions = LayoutOptions.Center;
        var row = new Grid { Padding = new Thickness(24, 10), MinimumHeightRequest = 68, ColumnSpacing = 14, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        row.Add(icon, 0);
        row.Add(details, 1);
        row.Add(arrow, 2);
        row.Add(button);
        Grid.SetColumnSpan(button, 3);
        return row;
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
