using MuscleForge.Client;

namespace MuscleForge.Mobile.Core.Authentication;

public sealed class ApiAuthGateway(MuscleForgeApiClient client) : IAuthGateway
{
    public async Task<AuthTokens> LoginAsync(Uri baseUri, string email, string password, CancellationToken cancellationToken)
    {
        var result = await client.LoginAsync(baseUri, new LoginRequest { Email = email, Password = password }, cancellationToken);
        return new(result.AccessToken, result.RefreshToken, result.ExpiresIn);
    }
    public async Task<AuthTokens> RefreshAsync(Uri baseUri, string refreshToken, CancellationToken cancellationToken)
    {
        var result = await client.RefreshAsync(baseUri, new RefreshRequest { RefreshToken = refreshToken }, cancellationToken);
        return new(result.AccessToken, result.RefreshToken, result.ExpiresIn);
    }
    public async Task<AuthAccount> GetCurrentUserAsync(Uri baseUri, string accessToken, CancellationToken cancellationToken)
    {
        var result = await client.GetCurrentUserAsync(baseUri, accessToken, cancellationToken);
        return new(result.Id, result.Email);
    }
}
