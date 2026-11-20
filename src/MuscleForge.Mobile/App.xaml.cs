namespace MuscleForge.Mobile;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new Views.RevampedPage())
        {
            BarBackgroundColor = Color.FromArgb("#14171E"),
            BarTextColor = Colors.White
        });
}
