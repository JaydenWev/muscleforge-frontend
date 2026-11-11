using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Catalog;

namespace MuscleForge.Mobile.Views.Catalog;

public sealed class ExercisePickerPage : ContentPage
{
    private readonly ExerciseSelectionViewModel viewModel;
    private readonly Func<ExerciseSelection?, Task> finish;
    private bool loaded;

    public ExercisePickerPage(ExerciseSelectionViewModel viewModel, Func<ExerciseSelection?, Task> finish)
    {
        this.viewModel = viewModel;
        this.finish = finish;
        var list = viewModel.List;
        BindingContext = list;
        Title = "Choose exercise";
        Shell.SetNavBarIsVisible(this, true);
        var search = new Entry { Placeholder = "Search name or equipment", ReturnType = ReturnType.Search };
        search.SetBinding(Entry.TextProperty, nameof(list.SearchText));
        search.Completed += (_, _) => list.SearchCommand.Execute(null);
        var error = ForgeUi.Text(""); error.SetBinding(Label.TextProperty, nameof(list.ErrorMessage));
        var selectionError = ForgeUi.Text(""); selectionError.SetBinding(Label.TextProperty, new Binding(nameof(viewModel.ErrorMessage), source: viewModel));
        var spinner = new ActivityIndicator(); spinner.SetBinding(ActivityIndicator.IsRunningProperty, nameof(list.IsBusy)); spinner.SetBinding(IsVisibleProperty, nameof(list.IsBusy));
        var selecting = new ActivityIndicator();
        selecting.SetBinding(ActivityIndicator.IsRunningProperty, new Binding(nameof(viewModel.IsBusy), source: viewModel));
        selecting.SetBinding(IsVisibleProperty, new Binding(nameof(viewModel.IsBusy), source: viewModel));
        var empty = ForgeUi.Text("No active exercises found."); empty.SetBinding(IsVisibleProperty, nameof(list.IsEmpty));
        var retry = new Button { Text = "Retry", Command = list.RetryCommand, Style = ForgeUi.Style("SecondaryButton") };
        retry.SetBinding(IsVisibleProperty, new Binding(nameof(list.ErrorMessage), converter: new HasTextConverter()));
        SemanticProperties.SetDescription(search, "Search exercises by name or equipment");
        var items = new CollectionView { ItemsSource = list.Items, ItemTemplate = new DataTemplate(() => ExerciseCatalogPage.ExerciseRow(SelectAsync, new MultiBinding { Converter = new EditorReadyConverter(), Bindings = { new Binding(nameof(list.IsBusy), source: list), new Binding(nameof(viewModel.IsBusy), source: viewModel) } })), SelectionMode = SelectionMode.None };
        var more = new Button { Text = "Load more", Command = list.LoadMoreCommand, Style = ForgeUi.Style("SecondaryButton") };
        more.SetBinding(IsVisibleProperty, nameof(list.HasMore));
        var cancelOperation = new Button { Text = "Cancel loading", Command = list.CancelCommand, Style = ForgeUi.Style("SecondaryButton") };
        cancelOperation.SetBinding(IsVisibleProperty, nameof(list.IsBusy));
        items.Header = new VerticalStackLayout { Padding = new Thickness(24, 20, 24, 12), Spacing = 12, Children = { ForgeUi.Header("YOUR LIBRARY", "Choose exercise", "Select an active exercise."), search, new Button { Text = "Search", Command = list.SearchCommand }, spinner, selecting, cancelOperation, error, selectionError, retry, empty } };
        items.Footer = new VerticalStackLayout { Padding = new Thickness(24, 12, 24, 32), Spacing = 12, Children = { more, ForgeUi.Button("Cancel", () => finish(null), secondary: true) } };
        Content = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star) }, Children = { items } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        loaded = true;
        try { await viewModel.List.LoadAsync(); } catch (Exception) { }
    }

    private async Task SelectAsync(Exercise exercise)
    {
        if (viewModel.List.IsBusy || viewModel.IsBusy || exercise.IsArchived) return;
        try
        {
            var selection = await viewModel.SelectAsync(exercise, CancellationToken.None);
            if (selection is not null) await finish(selection);
        }
        catch (Exception) { }
    }
}
