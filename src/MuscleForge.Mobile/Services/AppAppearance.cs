namespace MuscleForge.Mobile.Services;

public enum AppearanceMode { Dark, Light, System }

public sealed class AppAppearance
{
    private Application? application;

    public AppAppearance()
    {
        Mode = Enum.TryParse<AppearanceMode>(Preferences.Default.Get("appearance_mode", "Dark"), out var mode)
            ? mode : AppearanceMode.Dark;
    }

    public AppearanceMode Mode { get; private set; }
    public event EventHandler? Changed;

    public void Initialize(Application app)
    {
        application = app;
        app.RequestedThemeChanged += (_, _) => ApplyPalette();
        Apply();
    }

    public void SetMode(AppearanceMode mode)
    {
        Mode = mode;
        Preferences.Default.Set("appearance_mode", mode.ToString());
        Apply();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        if (application is null) return;
        application.UserAppTheme = Mode switch
        {
            AppearanceMode.Dark => AppTheme.Dark,
            AppearanceMode.Light => AppTheme.Light,
            _ => AppTheme.Unspecified
        };
        ApplyPalette();
    }

    private void ApplyPalette()
    {
        if (application is null) return;
        var dark = application.RequestedTheme == AppTheme.Dark;
        foreach (var (key, night, day) in Palette)
            application.Resources[key] = Color.FromArgb(dark ? night : day);
        UpdateSystemBars();
    }

    public static void UpdateSystemBars()
    {
#if ANDROID
        var window = Platform.CurrentActivity?.Window;
        if (window?.DecorView is not { } view) return;
        var light = Application.Current?.RequestedTheme != AppTheme.Dark;
        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, view);
        if (controller is null) return;
        controller.AppearanceLightStatusBars = light;
        controller.AppearanceLightNavigationBars = light;
#endif
    }

    private static readonly (string Key, string Dark, string Light)[] Palette =
    [
        ("PageBackground", "#0F1512", "#F5F7F1"),
        ("Surface", "#19221C", "#FFFFFF"),
        ("SurfaceRaised", "#222E25", "#EDF2E7"),
        ("Outline", "#304035", "#DEE6D7"),
        ("TextPrimary", "#F4F7EF", "#1C2B20"),
        ("TextSecondary", "#ABB9AA", "#586B59"),
        ("TextMuted", "#839480", "#64735F"),
        ("Accent", "#D6F78A", "#315E35"),
        ("AccentSoft", "#2B3D24", "#E7F0D9"),
        ("OnAccent", "#17200E", "#FFFFFF"),
        ("HeroBackground", "#203728", "#E0EDCF"),
        ("HeroText", "#F5FAEE", "#203824"),
        ("HeroMuted", "#B5C8AE", "#536E4C"),
        ("HeroArt", "#85A873", "#87A373"),
        ("Warning", "#F5CB8B", "#8B581B"),
        ("WarningSoft", "#3D3021", "#FFF1DD"),
        ("TabBackground", "#141D17", "#FFFFFF")
    ];
}
