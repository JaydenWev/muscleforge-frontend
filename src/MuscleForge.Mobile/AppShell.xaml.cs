using MuscleForge.Mobile.Views;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Views.Catalog;
using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Services;
using MuscleForge.Mobile.Views.Templates;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile;

public partial class AppShell : Shell, IDisposable
{
    private readonly ExerciseCatalogPage catalog;
    private readonly TemplateListPage templates;
    private readonly HashSet<Page> templatePages = [];
    public TemplateScope Scope { get; }
    public bool HasTemplateEditor => templates.Navigation.NavigationStack.Any(page => page is TemplateEditorPage);
    public event EventHandler? TemplateEditingEnded;

    public AppShell(ConnectionPage settingsPage, ConnectionViewModel connection, ExerciseCatalogPage catalog,
        TemplatePages templateFactory, ILocalWorkspace workspace)
    {
        this.catalog = catalog;
        InitializeComponent();
        WorkoutContent.Content = new WorkoutPage(connection);
        ExercisesContent.Content = catalog;
        Scope = workspace.CurrentScope ?? throw new InvalidOperationException("A verified workspace is required.");
        templates = templateFactory.CreateList(Scope);
        TemplatesContent.Content = templates;
        templatePages.Add(templates);
        Navigated += OnNavigated;
        HistoryContent.Content = new FeaturePlaceholderPage("History", "THE BIGGER PICTURE", "Your effort.\nYour story.",
            "Your completed sessions will live here once workout logging is available. Until then, make sure your server is ready for you.", TrainingArtKind.Progress);
        SettingsContent.Content = settingsPage;
    }
    private void OnNavigated(object? sender, ShellNavigatedEventArgs args)
    {
        var wasEditing = templatePages.Any(page => page is TemplateEditorPage);
        var current = templates.Navigation.NavigationStack.ToHashSet();
        foreach (var removed in templatePages.Where(page => !current.Contains(page) && page != templates).ToArray())
        {
            (removed as IDisposable)?.Dispose();
            templatePages.Remove(removed);
        }
        foreach (var page in current) templatePages.Add(page);
        if (wasEditing && !HasTemplateEditor) TemplateEditingEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Navigated -= OnNavigated;
        catalog.Dispose();
        foreach (var page in templatePages.OfType<IDisposable>()) page.Dispose();
        templatePages.Clear();
        TemplateEditingEnded = null;
    }
}
