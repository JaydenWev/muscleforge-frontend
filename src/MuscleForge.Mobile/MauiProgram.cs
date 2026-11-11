using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Views.Catalog;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Services;
using MuscleForge.Mobile.Views;

namespace MuscleForge.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Regular.ttf", "Manrope");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemiBold");
                fonts.AddFont("Manrope-ExtraBold.ttf", "ManropeExtraBold");
            });
        builder.Services.AddSingleton<AppAppearance>();
        builder.Services.AddHttpClient<MuscleForgeApiClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton<IConnectionSettings, ConnectionSettings>();
        builder.Services.AddTransient<IApiStatusService, ApiStatusService>();
        builder.Services.AddSingleton<ConnectionViewModel>();
        builder.Services.AddSingleton<IRefreshCredentialStore, SecureRefreshCredentialStore>();
        builder.Services.AddTransient<IAuthGateway, ApiAuthGateway>();
        builder.Services.AddSingleton<IAuthSession, AuthSession>();
        builder.Services.AddTransient<IExerciseCatalogGateway, ApiExerciseCatalogGateway>();
        builder.Services.AddTransient<ExerciseListViewModel>(services => new ExerciseListViewModel(services.GetRequiredService<IExerciseCatalogGateway>(), services.GetRequiredService<IAuthSession>(), MainThread.BeginInvokeOnMainThread));
        builder.Services.AddTransient<ExerciseNavigation>();
        builder.Services.AddSingleton<IExercisePicker, ExercisePicker>();
        builder.Services.AddTransient<ExerciseCatalogPage>();
        builder.Services.AddSingleton<SessionNavigator>();
        builder.Services.AddTransient<ConnectionPage>();
        builder.Services.AddTransient<AppShell>();
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ForgeEntry", (handler, _) =>
            {
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                {
                    if (handler.VirtualView.AutomationId == "SignInEmail") handler.PlatformView.SetAutofillHints("username", "emailAddress");
                    if (handler.VirtualView.AutomationId == "SignInPassword") handler.PlatformView.SetAutofillHints("password");
                }
            });
#endif
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
