using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Views.Templates;

public sealed class TemplateListPage : ContentPage, IDisposable
{
    private readonly TemplateListViewModel viewModel;
    private readonly Func<Guid?, Task> openTemplate;
    private bool opening, disposed;
    private readonly CancellationTokenSource lifetime = new();

    public TemplateListPage(TemplateListViewModel viewModel, Func<Guid?, Task> openTemplate)
    {
        this.viewModel = viewModel;
        this.openTemplate = openTemplate;
        BindingContext = viewModel;
        Title = "Templates";
        var add = ForgeUi.Button("+ Add template", () => OpenAsync(null));
        add.AutomationId = "TemplateAdd"; add.MinimumHeightRequest = 48;
        var archived = new Switch { AutomationId = "TemplateShowArchived", MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
        SemanticProperties.SetDescription(archived, "Show archived templates");
        archived.SetBinding(Switch.IsToggledProperty, nameof(viewModel.IncludeArchived));
        archived.Toggled += async (_, _) => { if (IsLoaded) await LoadAsync(); };
        var busy = new ActivityIndicator(); busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(viewModel.IsBusy)); busy.SetBinding(IsVisibleProperty, nameof(viewModel.IsBusy));
        var error = ForgeUi.Text(""); error.SetBinding(Label.TextProperty, nameof(viewModel.ErrorMessage));
        var empty = ForgeUi.Text("No templates yet. Add a template to plan a workout."); empty.SetBinding(IsVisibleProperty, nameof(viewModel.IsEmpty));
        var retry = ForgeUi.Button("Retry", LoadAsync, true); retry.AutomationId = "TemplateRetry";
        var more = ForgeUi.Button("Load more", async () => { if (!disposed) await viewModel.LoadMoreAsync(lifetime.Token); }, true); more.AutomationId = "TemplateLoadMore"; more.SetBinding(IsVisibleProperty, nameof(viewModel.HasMore));
        var items = new CollectionView { ItemsSource = viewModel.Items, SelectionMode = SelectionMode.None, AutomationId = "TemplateList", ItemTemplate = new DataTemplate(Row) };
        items.Header = new VerticalStackLayout { Padding = new Thickness(24, 20, 24, 12), Spacing = 12, Children = { ForgeUi.Header("YOUR LIBRARY", "Templates", "Your templates are saved on this device."), add, new HorizontalStackLayout { Spacing = 8, Children = { archived, ForgeUi.Text("Show archived", "SmallText") } }, busy, error, retry, empty } };
        items.Footer = new VerticalStackLayout { Padding = new Thickness(24, 12, 24, 32), Children = { more } };
        Content = items;
    }

    private View Row()
    {
        var name = ForgeUi.Text("", "SectionTitle"); name.SetBinding(Label.TextProperty, nameof(LocalTemplateSummary.Name));
        var status = ForgeUi.Text("ARCHIVED", "Eyebrow"); status.SetBinding(IsVisibleProperty, nameof(LocalTemplateSummary.IsArchived));
        var button = new Button { Text = "Open", MinimumHeightRequest = 48, AutomationId = "TemplateOpen" };
        button.Clicked += async (sender, _) => { if (((Button)sender!).BindingContext is LocalTemplateSummary item) await OpenAsync(item.Id); };
        var row = new Grid { Padding = new Thickness(24, 10), ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        row.Add(new VerticalStackLayout { Children = { name, status }, VerticalOptions = LayoutOptions.Center }); row.Add(button, 1);
        return row;
    }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync() { if (disposed) return; try { await viewModel.LoadAsync(lifetime.Token); } catch (Exception) { } }
    private async Task OpenAsync(Guid? id) { if (opening || viewModel.IsBusy) return; opening = true; try { await openTemplate(id); } finally { opening = false; } }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
