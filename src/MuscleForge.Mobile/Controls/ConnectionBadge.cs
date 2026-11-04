using Microsoft.Maui.Controls.Shapes;
using MuscleForge.Mobile.Core.ViewModels;

namespace MuscleForge.Mobile.Controls;

public sealed class ConnectionBadge : ContentView
{
    public static readonly BindableProperty StateProperty = BindableProperty.Create(nameof(State), typeof(ConnectionState), typeof(ConnectionBadge), ConnectionState.NotConfigured, propertyChanged: (bindable, _, _) => ((ConnectionBadge)bindable).Refresh());
    private readonly Label label;
    private readonly BoxView dot;
    private readonly Border badge;

    public ConnectionBadge()
    {
        label = new Label { FontFamily = "ManropeSemiBold", FontSize = 10, VerticalOptions = LayoutOptions.Center };
        dot = new BoxView { WidthRequest = 5, HeightRequest = 5, CornerRadius = 3, VerticalOptions = LayoutOptions.Center };
        badge = new Border
        {
            Padding = new Thickness(10, 6), StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new HorizontalStackLayout { Spacing = 6, Children = { dot, label } }
        };
        Content = badge;
        HorizontalOptions = LayoutOptions.Start;
        Refresh();
    }

    public ConnectionState State { get => (ConnectionState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    private void Refresh()
    {
        var positive = State == ConnectionState.Connected;
        var error = State is ConnectionState.InvalidAddress or ConnectionState.Unavailable or ConnectionState.Incompatible or ConnectionState.Error;
        var tint = positive ? "Accent" : error ? "Warning" : "TextSecondary";
        badge.SetDynamicResource(VisualElement.BackgroundColorProperty, positive ? "AccentSoft" : error ? "WarningSoft" : "SurfaceRaised");
        label.SetDynamicResource(Label.TextColorProperty, tint);
        dot.SetDynamicResource(VisualElement.BackgroundColorProperty, tint);
        label.Text = State switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Checking => "Checking",
            ConnectionState.Ready => "Not checked",
            ConnectionState.NotConfigured => "Not connected",
            _ => "Needs attention"
        };
    }
}
