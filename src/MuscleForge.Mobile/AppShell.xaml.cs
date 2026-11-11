using MuscleForge.Mobile.Views;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Views.Catalog;

namespace MuscleForge.Mobile;

public partial class AppShell : Shell, IDisposable
{
    private readonly ExerciseCatalogPage catalog;

    public AppShell(ConnectionPage settingsPage, ConnectionViewModel connection, ExerciseCatalogPage catalog)
    {
        this.catalog = catalog;
        InitializeComponent();
        WorkoutContent.Content = new WorkoutPage(connection);
        ExercisesContent.Content = catalog;
        TemplatesContent.Content = new FeaturePlaceholderPage("Templates", "YOUR LIBRARY", "Good plans.\nGreat sessions.",
            "A home for your go-to routines. Template creation will be available in a future update. For now, get your server connected in Settings.", TrainingArtKind.Plans);
        HistoryContent.Content = new FeaturePlaceholderPage("History", "THE BIGGER PICTURE", "Your effort.\nYour story.",
            "Your completed sessions will live here once workout logging is available. Until then, make sure your server is ready for you.", TrainingArtKind.Progress);
        SettingsContent.Content = settingsPage;
    }
    public void Dispose() => catalog.Dispose();
}
