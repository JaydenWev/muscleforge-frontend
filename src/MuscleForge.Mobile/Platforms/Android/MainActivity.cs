using Android.App;
using Android.Content.PM;
using Android.OS;
#if DEBUG
using Android.Content;
#endif
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if DEBUG
        StartWithWeightVerification = IsWeightVerificationIntent(Intent);
#endif
        base.OnCreate(savedInstanceState);
        AppAppearance.UpdateSystemBars();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppAppearance.UpdateSystemBars();
    }

#if DEBUG
    internal static bool StartWithWeightVerification { get; private set; }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (IsWeightVerificationIntent(intent) && Microsoft.Maui.Controls.Application.Current is App app)
            app.OpenWeightVerification();
    }

    private static bool IsWeightVerificationIntent(Intent? intent) =>
        intent?.Action == "local.muscleforge.app.DEBUG_WEIGHT_ENTRY";
#endif
}
