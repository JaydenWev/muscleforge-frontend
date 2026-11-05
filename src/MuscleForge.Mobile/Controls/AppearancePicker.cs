using Microsoft.Maui.Controls.Shapes;
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile.Controls;

public sealed class AppearancePicker : ContentView
{
    private readonly Dictionary<AppearanceMode, (Border Border, IconView Icon, Label Label)> options = [];
    private AppAppearance? appearance;

    public void Initialize(AppAppearance service)
    {
        appearance = service;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 };
        var modes = new[] { (AppearanceMode.Dark, ForgeIcon.Moon), (AppearanceMode.Light, ForgeIcon.Sun), (AppearanceMode.System, ForgeIcon.System) };
        for (var i = 0; i < modes.Length; i++)
        {
            var (mode, glyph) = modes[i];
            var icon = ForgeUi.Icon(glyph, 23);
            icon.HorizontalOptions = LayoutOptions.Center;
            var label = ForgeUi.Text(mode.ToString(), "SmallText");
            label.FontFamily = "ManropeSemiBold";
            label.HorizontalTextAlignment = TextAlignment.Center;
            var border = new Border
            {
                StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(8, 15), MinimumHeightRequest = 78,
                Content = new VerticalStackLayout { Spacing = 8, Children = { icon, label } },
                AutomationId = "Appearance" + mode
            };
            SemanticProperties.SetDescription(border, mode + " appearance");
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => service.SetMode(mode);
            border.GestureRecognizers.Add(tap);
            grid.Add(border, i);
            options[mode] = (border, icon, label);
        }
        Content = grid;
        service.Changed += OnAppearanceChanged;
        Unloaded += (_, _) => service.Changed -= OnAppearanceChanged;
        Loaded += (_, _) => { service.Changed -= OnAppearanceChanged; service.Changed += OnAppearanceChanged; Refresh(); };
        Refresh();
    }

    private void OnAppearanceChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        foreach (var (mode, option) in options)
        {
            var selected = appearance?.Mode == mode;
            option.Border.SetDynamicResource(VisualElement.BackgroundColorProperty, selected ? "AccentSoft" : "SurfaceRaised");
            option.Border.SetDynamicResource(Border.StrokeProperty, selected ? "Accent" : "Outline");
            option.Icon.SetDynamicResource(IconView.TintProperty, selected ? "Accent" : "TextSecondary");
            option.Label.SetDynamicResource(Label.TextColorProperty, selected ? "Accent" : "TextSecondary");
            SemanticProperties.SetHint(option.Border, selected ? "Selected" : "Double tap to select");
        }
    }
}
