using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Networking;
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
        builder.Services.AddTransient<ConnectionPage>();
        builder.Services.AddTransient<AppShell>();
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ForgeEntry", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
