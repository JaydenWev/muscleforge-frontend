using MuscleForge.Mobile.Controls;

namespace MuscleForge.Mobile.Views;

public sealed class FeaturePlaceholderPage : ContentPage
{
    public FeaturePlaceholderPage(string title, string eyebrow, string heading, string description, TrainingArtKind artwork)
    {
        Title = title;
        var headingLabel = ForgeUi.Text(heading, "DisplayTitle");
        SemanticProperties.SetHeadingLevel(headingLabel, SemanticHeadingLevel.Level1);
        var illustration = new TrainingArt { Kind = artwork, HeightRequest = 220, Margin = new Thickness(0, 4) };
        var message = new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                illustration,
                ForgeUi.Pill("COMING SOON"),
                ForgeUi.Text(title == "Templates" ? "Your routine starts here." : "Good things take reps.", "SectionTitle"),
                ForgeUi.Text(description),
                ForgeUi.Button("Open settings  →", () => Shell.Current.GoToAsync("//settings"), secondary: true)
            }
        };
        Content = ForgeUi.PageContent(new VerticalStackLayout { Spacing = 12, Children = { ForgeUi.Text(eyebrow, "Eyebrow"), headingLabel } }, ForgeUi.Card(message));
    }
}
