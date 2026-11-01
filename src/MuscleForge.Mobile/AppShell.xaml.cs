using MuscleForge.Mobile.Views;

namespace MuscleForge.Mobile;

public partial class AppShell : Shell
{
    public AppShell(ConnectionPage settingsPage)
    {
        InitializeComponent();
        WorkoutContent.Content = new FeaturePlaceholderPage("Workout", "Your training, one set at a time.",
            "Workout logging is coming next. Open Settings to connect your API.");
        TemplatesContent.Content = new FeaturePlaceholderPage("Templates", "Build your routine.",
            "Workout planning will appear here as the project develops.");
        HistoryContent.Content = new FeaturePlaceholderPage("History", "See your progress.",
            "Completed workouts will appear here when logging is available.");
        SettingsContent.Content = settingsPage;
    }
}
