using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.Workspaces;
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile.Views;

public sealed class LocalSettingsPage : ContentPage
{
    public LocalSettingsPage(AppAppearance appearance, IGuestWorkspace workspace, ILegacyTemplateMigration migration)
    {
        Title = "Settings";
        var themes = new AppearancePicker();
        themes.Initialize(appearance);
        var import = ForgeUi.Button("Existing libraries", async () =>
        {
            if (workspace.CurrentScope is not { } scope) return;
            await Navigation.PushAsync(new ExistingLibrariesPage(migration, scope));
        }, true);
        import.AutomationId = "ExistingLibraries";
        var notice = ForgeUi.Text(workspace.Notice ?? "");
        notice.IsVisible = !string.IsNullOrEmpty(workspace.Notice);
        Content = ForgeUi.PageContent(
            ForgeUi.Header("MAKE YOURSELF AT HOME", "Your space,\nyour way.", "Your template library works on this device without an account or connection."),
            ForgeUi.Card(new VerticalStackLayout { Spacing = 16, Children = { ForgeUi.Text("Set the mood", "SectionTitle"), themes } }),
            ForgeUi.Card(new VerticalStackLayout
            {
                Spacing = 16,
                Children =
                {
                    ForgeUi.Text("On this device", "SectionTitle"),
                    ForgeUi.Text("Local data is retained when account connections change. Uninstalling or clearing app data removes it; local storage is not a backup."),
                    notice, import
                }
            }));
    }
}
