using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        this.services = services;
        services.GetRequiredService<AppAppearance>().Initialize(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigator = services.GetRequiredService<SessionNavigator>();
#if DEBUG && ANDROID
        if (MainActivity.StartWithWeightVerification) navigator.OpenWeightVerification();
#endif
        return navigator.CreateWindow();
    }

#if DEBUG && ANDROID
    internal void OpenWeightVerification() => services.GetRequiredService<SessionNavigator>().OpenWeightVerification();
#endif
}
