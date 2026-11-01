using MuscleForge.Client;

namespace MuscleForge.Mobile.Core.Networking;

public interface IApiStatusService
{
    Task<ApiStatusResponse> GetStatusAsync(Uri endpoint, CancellationToken cancellationToken);
}

public sealed class ApiStatusService(MuscleForgeApiClient client) : IApiStatusService
{
    public Task<ApiStatusResponse> GetStatusAsync(Uri endpoint, CancellationToken cancellationToken) =>
        client.GetApiStatusAsync(endpoint, cancellationToken);
}
