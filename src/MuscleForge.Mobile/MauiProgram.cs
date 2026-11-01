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
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddHttpClient<MuscleForgeApiClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton<IConnectionSettings, ConnectionSettings>();
        builder.Services.AddTransient<IApiStatusService, ApiStatusService>();
        builder.Services.AddTransient<ConnectionViewModel>();
        builder.Services.AddTransient<ConnectionPage>();
        builder.Services.AddTransient<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
