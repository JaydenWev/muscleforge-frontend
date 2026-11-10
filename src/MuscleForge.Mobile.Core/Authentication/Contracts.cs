namespace MuscleForge.Mobile.Core.Authentication;

public enum AuthStatus { SignedOut, Restoring, SigningIn, SignedIn, SigningOut, RequiresSignIn, StorageError, ConnectionError }
public sealed record AuthAccount(Guid Id, string Email);
public sealed record SessionState(AuthStatus Status, AuthAccount? Account = null, string? Message = null);

public sealed class AuthContext(Uri baseUri, Guid accountId, string accessToken)
{
    public Uri BaseUri { get; } = baseUri;
    public Guid AccountId { get; } = accountId;
    public string AccessToken { get; } = accessToken;
    public override string ToString() => $"AuthContext {{ BaseUri = {BaseUri}, AccountId = {AccountId} }}";
}

// The adapter stores exactly one opaque value under its own secure-storage key.
public interface IRefreshCredentialStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(string value, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthTokens(string accessToken, string refreshToken, long expiresIn)
{
    public string AccessToken { get; } = accessToken;
    public string RefreshToken { get; } = refreshToken;
    public long ExpiresIn { get; } = expiresIn;
    public override string ToString() => "AuthTokens { [redacted] }";
}

public interface IAuthGateway
{
    Task<AuthTokens> LoginAsync(Uri baseUri, string email, string password, CancellationToken cancellationToken);
    Task<AuthTokens> RefreshAsync(Uri baseUri, string refreshToken, CancellationToken cancellationToken);
    Task<AuthAccount> GetCurrentUserAsync(Uri baseUri, string accessToken, CancellationToken cancellationToken);
}

public interface IAuthSession
{
    SessionState State { get; }
    event EventHandler<SessionState>? StateChanged;
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task SignInAsync(string email, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default);
    Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default);
}
