namespace MuscleForge.Mobile.Core.Networking;

public static class ApiEndpoint
{
    public static bool TryParse(string? value, out Uri? endpoint)
    {
        endpoint = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(candidate.Host)
            || candidate.UserInfo.Length != 0
            || candidate.Query.Length != 0
            || candidate.Fragment.Length != 0)
        {
            return false;
        }

        endpoint = new Uri(candidate.AbsoluteUri.TrimEnd('/') + "/");
        return true;
    }
}
