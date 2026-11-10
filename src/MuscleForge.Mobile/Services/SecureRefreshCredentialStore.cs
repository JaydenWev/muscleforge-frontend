using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Services;

public sealed class SecureRefreshCredentialStore : IRefreshCredentialStore
{
    private const string Key = "muscleforge.auth.refresh.v1";
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { var value = await SecureStorage.Default.GetAsync(Key); cancellationToken.ThrowIfCancellationRequested(); return value; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new InvalidOperationException("Secure storage is unavailable."); }
    }
    public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { await SecureStorage.Default.SetAsync(Key, value); cancellationToken.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new InvalidOperationException("Secure storage is unavailable."); }
    }
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
        catch (Exception) { throw new InvalidOperationException("Secure storage is unavailable."); }
    }
}
