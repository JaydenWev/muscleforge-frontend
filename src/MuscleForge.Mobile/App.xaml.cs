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

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(services.GetRequiredService<AppShell>());
}
