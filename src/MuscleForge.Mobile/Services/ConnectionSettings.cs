using MuscleForge.Mobile.Core.Settings;

namespace MuscleForge.Mobile.Services;

public sealed class ConnectionSettings : IConnectionSettings
{
    public string ApiBaseUrl
    {
        get => Preferences.Default.Get("api_base_url", string.Empty);
        set => Preferences.Default.Set("api_base_url", value);
    }
}
