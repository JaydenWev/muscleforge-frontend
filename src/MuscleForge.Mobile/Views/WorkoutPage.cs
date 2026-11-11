using Microsoft.Maui.Controls.Shapes;
using MuscleForge.Mobile.Controls;
using MuscleForge.Mobile.Core.ViewModels;

namespace MuscleForge.Mobile.Views;

public sealed class WorkoutPage : ContentPage
{
    public WorkoutPage(ConnectionViewModel connection)
    {
        Title = "Workout";
        BindingContext = connection;

        var wordmark = ForgeUi.Text("MuscleForge", "SectionTitle");
        wordmark.FontSize = 19;
        var brand = new HorizontalStackLayout
        {
            Spacing = 12,
            Children =
            {
                ForgeUi.IconTile(ForgeIcon.Dumbbell, 46),
                new VerticalStackLayout
                {
                    Spacing = 2, VerticalOptions = LayoutOptions.Center,
                    Children = { wordmark, ForgeUi.Text("YOUR TRAINING COMPANION", "Eyebrow") }
                }
            }
        };

        var heading = ForgeUi.Text("Stronger.\nEvery day.", "DisplayTitle");
        heading.FontSize = 38;
        heading.SetDynamicResource(Label.TextColorProperty, "HeroText");
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var kicker = ForgeUi.Text("BUILT ONE SESSION AT A TIME", "Eyebrow");
        kicker.FontSize = 9;
        kicker.SetDynamicResource(Label.TextColorProperty, "HeroMuted");
        var intro = ForgeUi.Text("A little focus. A little consistency.\nA stronger version of you.");
        intro.SetDynamicResource(Label.TextColorProperty, "HeroMuted");
        var heroContent = new VerticalStackLayout
        {
            Spacing = 18, Padding = 24,
            Children =
            {
                kicker, heading, intro,
                ForgeUi.Button("Set up your space  →", () => Shell.Current.GoToAsync("//settings"))
            }
        };
        var hero = new Border
        {
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 28 },
            Content = new Grid
            {
                IsClippedToBounds = true,
                Children =
                {
                    new TrainingArt { WidthRequest = 210, HeightRequest = 210, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 24, -42, 0), Opacity = .22 },
                    heroContent
                }
            }
        };
        hero.SetDynamicResource(BackgroundColorProperty, "HeroBackground");

        var explore = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
        explore.Add(Destination("Templates", "A place for\nyour next plan.", ForgeIcon.Templates, "templates"), 0);
        explore.Add(Destination("History", "Every session.\nPart of your story.", ForgeIcon.History, "history"), 1);

        var badge = new ConnectionBadge();
        badge.SetBinding(ConnectionBadge.StateProperty, nameof(ConnectionViewModel.State));
        var serverName = ForgeUi.Text("");
        serverName.FontFamily = "ManropeSemiBold";
        serverName.LineBreakMode = LineBreakMode.MiddleTruncation;
        serverName.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        serverName.SetBinding(Label.TextProperty, nameof(ConnectionViewModel.ServerHost));
        var server = ForgeUi.Card(new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children = { ForgeUi.Text("YOUR SERVER", "Eyebrow"), WithColumn(badge, 1) }
                },
                serverName,
                ForgeUi.Text("Keep your server close. Manage your address and check its connection in Settings.", "SmallText"),
                ForgeUi.Button("Connection settings  →", () => Shell.Current.GoToAsync("//settings"), secondary: true)
            }
        });

        var catalog = ForgeUi.Card(new VerticalStackLayout { Spacing = 12, Children = { ForgeUi.Text("Your exercises", "SectionTitle"), ForgeUi.Text("Manage the movements in your catalog."), ForgeUi.Button("Open exercises  →", () => Shell.Current.GoToAsync("//exercises"), secondary: true) } });

        Content = ForgeUi.PageContent(brand, hero, catalog,
            new VerticalStackLayout { Spacing = 14, Children = { ForgeUi.Text("Make it your routine", "SectionTitle"), explore } }, server);
    }

    private static View WithColumn(View view, int column) { Grid.SetColumn(view, column); return view; }

    private static Border Destination(string title, string detail, ForgeIcon icon, string route)
    {
        var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        top.Add(ForgeUi.Icon(icon, 26));
        top.Add(ForgeUi.Icon(ForgeIcon.ArrowRight, 18, "TextMuted"), 1);
        var name = ForgeUi.Text(title, "SectionTitle");
        name.FontSize = 17;
        var card = ForgeUi.Card(new VerticalStackLayout { Spacing = 14, Children = { top, name, ForgeUi.Text(detail, "SmallText") } }, new Thickness(18));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync("//" + route);
        card.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(card, "Open " + title);
        return card;
    }
}
