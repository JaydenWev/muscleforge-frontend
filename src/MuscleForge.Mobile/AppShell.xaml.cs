using MuscleForge.Mobile.Views;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Services;
using MuscleForge.Mobile.Views.Templates;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile;

public partial class AppShell : Shell, IDisposable
{
    private readonly TemplateListPage templates;
    private readonly HashSet<Page> templatePages = [];
    public TemplateScope Scope { get; }
    public bool HasTemplateEditor => templates.Navigation.NavigationStack.Any(page => page is TemplateEditorPage);
    public event EventHandler? TemplateEditingEnded;

    public AppShell(LocalSettingsPage settingsPage, TemplatePages templateFactory, IGuestWorkspace workspace)
    {
        InitializeComponent();
        WorkoutContent.Content = new WorkoutPage();
        ExercisesContent.Content = new FeaturePlaceholderPage("Exercises", "YOUR MOVEMENTS", "Made for\nyour training.",
            "Exercise details saved in your templates are available when editing a plan. A full on-device exercise catalog is coming next.", TrainingArtKind.Plans);
        Scope = workspace.CurrentScope ?? throw new InvalidOperationException("The local workspace is not ready.");
        templates = templateFactory.CreateList(Scope);
        TemplatesContent.Content = templates;
        templatePages.Add(templates);
        Navigated += OnNavigated;
        HistoryContent.Content = new FeaturePlaceholderPage("History", "THE BIGGER PICTURE", "Your effort.\nYour story.",
            "Your completed sessions will live here once local workout logging is available. You can prepare your templates now.", TrainingArtKind.Progress);
        SettingsContent.Content = settingsPage;
    }
    private void OnNavigated(object? sender, ShellNavigatedEventArgs args)
    {
        var wasEditing = templatePages.Any(page => page is TemplateEditorPage);
        var current = templates.Navigation.NavigationStack.ToHashSet();
        if (SettingsContent.Content is Page settings)
            current.UnionWith(settings.Navigation.NavigationStack);
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
        (SettingsContent.Content as IDisposable)?.Dispose();
        foreach (var page in templatePages.OfType<IDisposable>()) page.Dispose();
        templatePages.Clear();
        TemplateEditingEnded = null;
    }
}
