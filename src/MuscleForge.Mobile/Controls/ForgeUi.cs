using Microsoft.Maui.Controls.Shapes;

namespace MuscleForge.Mobile.Controls;

internal static class ForgeUi
{
    public static Style Style(string key) => (Style)Application.Current!.Resources[key];

    public static Label Text(string text, string style = "BodyText") => new() { Text = text, Style = Style(style) };

    public static IconView Icon(ForgeIcon kind, double size = 24, string color = "Accent")
    {
        var icon = new IconView { Kind = kind, WidthRequest = size, HeightRequest = size };
        icon.SetDynamicResource(IconView.TintProperty, color);
        return icon;
    }

    public static Border IconTile(ForgeIcon kind, double size = 44)
    {
        var border = new Border
        {
            WidthRequest = size, HeightRequest = size, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Content = Icon(kind, 23), Padding = 10
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "AccentSoft");
        return border;
    }

    public static Border Card(View content, Thickness? padding = null) => new()
    {
        Style = Style("Card"), Content = content, Padding = padding ?? new Thickness(20)
    };

    public static VerticalStackLayout Header(string eyebrow, string title, string description) => new()
    {
        Spacing = 10,
        Children = { Text(eyebrow, "Eyebrow"), Text(title, "DisplayTitle"), Text(description) }
    };

    public static ScrollView PageContent(params View[] views)
    {
        var content = new VerticalStackLayout { Padding = new Thickness(24, 20, 24, 32), Spacing = 26 };
        foreach (var view in views) content.Children.Add(view);
        return new ScrollView { Content = content };
    }

    public static Button Button(string text, Func<Task> action, bool secondary = false)
    {
        var button = new Button { Text = text };
        if (secondary) button.Style = Style("SecondaryButton");
        button.Clicked += async (_, _) => await action();
        return button;
    }

    public static Border Pill(string text)
    {
        var label = Text(text, "Eyebrow");
        label.FontSize = 9;
        label.CharacterSpacing = 1.3;
        label.SetDynamicResource(Label.TextColorProperty, "Accent");
        var border = new Border
        {
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(10, 6), HorizontalOptions = LayoutOptions.Start, Content = label
        };
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, "AccentSoft");
        return border;
    }
}
