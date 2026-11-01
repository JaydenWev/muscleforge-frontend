namespace MuscleForge.Mobile.Views;

public sealed class FeaturePlaceholderPage : ContentPage
{
    public FeaturePlaceholderPage(string title, string heading, string description)
    {
        Title = title;
        var headingLabel = new Label { Text = heading, FontSize = 28, FontAttributes = FontAttributes.Bold };
        SemanticProperties.SetHeadingLevel(headingLabel, SemanticHeadingLevel.Level1);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 32),
                Spacing = 20,
                Children =
                {
                    new Label { Text = "MUSCLEFORGE", FontSize = 13, CharacterSpacing = 2 },
                    headingLabel,
                    new Label { Text = description, LineBreakMode = LineBreakMode.WordWrap }
                }
            }
        };
    }
}
