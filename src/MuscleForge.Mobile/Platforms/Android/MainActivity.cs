using Android.App;
using Android.Content.PM;
using Android.OS;
#if DEBUG
using Android.Content;
using MuscleForge.Mobile.Views;
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
        base.OnCreate(savedInstanceState);
        AppAppearance.UpdateSystemBars();
#if DEBUG
        OpenWeightVerification(Intent);
#endif
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppAppearance.UpdateSystemBars();
    }

#if DEBUG
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenWeightVerification(intent);
    }

    private static void OpenWeightVerification(Intent? intent)
    {
        if (intent?.Action != "local.muscleforge.app.DEBUG_WEIGHT_ENTRY") return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (window is not null) window.Page = new NavigationPage(new DebugWeightEntryPage());
        });
    }
#endif
}
