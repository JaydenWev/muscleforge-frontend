using System.Net;
using System.Text.Json;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;

namespace MuscleForge.Mobile.Core.Authentication;

public sealed class AuthSession(IConnectionSettings settings, IRefreshCredentialStore store, IAuthGateway gateway, TimeProvider? timeProvider = null) : IAuthSession
{
    private readonly object sync = new();
    // Serialize credential reads, writes, and clears so an older operation cannot overwrite a newer one.
    private readonly SemaphoreSlim storageGate = new(1, 1);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private SessionState state = new(AuthStatus.SignedOut);
    private AuthContext? context;
    private string? refreshToken;
    private DateTimeOffset expiresAt;
    // Each session transition invalidates completions from earlier operations.
    private long generation;
    private Task<AuthContext?>? refreshTask;
    public SessionState State
    {
        get { lock (sync) return state; }
    }
    public event EventHandler<SessionState>? StateChanged;

    private void Publish(SessionState value)
    {
        state = value;
        StateChanged?.Invoke(this, value);
    }
    private Uri? Endpoint() => ApiEndpoint.TryParse(settings.ApiBaseUrl, out var uri) ? uri : null;
    private bool Current(long version, Uri endpoint)
    {
        if (generation != version) return false;
        if (Endpoint() == endpoint) return true;
        generation++;
        context = null;
        refreshToken = null;
        refreshTask = null;
        Publish(new(AuthStatus.RequiresSignIn, null, "The server changed. Please sign in again."));
        return false;
    }
    private long Begin(AuthStatus status)
    {
        lock (sync)
        {
            generation++;
            context = null;
            refreshToken = null;
            refreshTask = null;
            Publish(new(status));
            return generation;
        }
    }

    private void Fail(long version, Uri? endpoint, AuthStatus status, string? message)
    {
        lock (sync)
        {
            if (generation == version && (endpoint is null || Current(version, endpoint)))
            {
                context = null;
                refreshToken = null;
                Publish(new(status, null, message));
            }
        }
    }

    public async Task SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var version = Begin(AuthStatus.SigningIn);
        var endpoint = Endpoint();
        if (endpoint is null)
        {
            Fail(version, null, AuthStatus.ConnectionError, "Enter a valid HTTPS server address.");
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || string.IsNullOrEmpty(password) || password.Length > 4096)
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "Enter a valid email and password.");
            return;
        }
        try
        {
            var start = clock.GetUtcNow();
            var tokens = await gateway.LoginAsync(endpoint, email, password, cancellationToken);
            await CompleteAsync(version, endpoint, tokens, null, start, cancellationToken);
        }
        catch (CredentialCleanupException)
        {
            Fail(version, endpoint, AuthStatus.StorageError, "Unable to clear saved sign-in.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(version, endpoint, AuthStatus.SignedOut, "Sign-in was cancelled.");
            throw;
        }
        catch (ApiException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "Sign-in failed. Check your credentials.");
        }
        catch (Exception)
        {
            Fail(version, endpoint, AuthStatus.ConnectionError, "Unable to connect to the server.");
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var version = Begin(AuthStatus.Restoring);
        var endpoint = Endpoint();
        if (endpoint is null)
        {
            Fail(version, null, AuthStatus.ConnectionError, "Enter a valid HTTPS server address.");
            return;
        }
        string? value;
        try
        {
            await storageGate.WaitAsync(cancellationToken);
            try
            {
                value = await store.ReadAsync(cancellationToken);
            }
            finally
            {
                storageGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(version, endpoint, AuthStatus.SignedOut, "Restore was cancelled.");
            throw;
        }
        catch
        {
            Fail(version, endpoint, AuthStatus.StorageError, "Unable to read saved sign-in.");
            return;
        }
        if (value is null)
        {
            Fail(version, endpoint, AuthStatus.SignedOut, null);
            return;
        }
        Credential? credential;
        try { credential = value.Length <= 32768 ? JsonSerializer.Deserialize<Credential>(value) : null; }
        catch (JsonException)
        {
            credential = null;
        }
        if (credential is null || credential.Version != 1 || credential.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(credential.RefreshToken) || credential.RefreshToken.Length > 16384 || credential.Server is null || credential.Server.Length > 2048 || !ApiEndpoint.TryParse(credential.Server, out var saved) || saved != endpoint)
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "Saved sign-in is invalid or belongs to another server.");
            return;
        }
        try
        {
            lock (sync)
            {
                if (!Current(version, endpoint)) return;
            }
            var start = clock.GetUtcNow();
            var tokens = await gateway.RefreshAsync(endpoint, credential.RefreshToken, cancellationToken);
            await CompleteAsync(version, endpoint, tokens, credential.AccountId, start, cancellationToken);
        }
        catch (CredentialCleanupException)
        {
            Fail(version, endpoint, AuthStatus.StorageError, "Unable to clear saved sign-in.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(version, endpoint, AuthStatus.SignedOut, "Restore was cancelled.");
            throw;
        }
        catch (ApiException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "Please sign in again.");
        }
        catch
        {
            Fail(version, endpoint, AuthStatus.ConnectionError, "Unable to restore sign-in. Try again when connected.");
        }
    }

    private async Task<AuthContext?> CompleteAsync(long version, Uri endpoint, AuthTokens tokens, Guid? expectedId, DateTimeOffset start, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokens.AccessToken) || string.IsNullOrWhiteSpace(tokens.RefreshToken) || tokens.AccessToken.Length > 16384 || tokens.RefreshToken.Length > 16384 || tokens.ExpiresIn is < 1 or > 86400)
            throw new InvalidOperationException("Invalid server response.");
        lock (sync)
        {
            if (!Current(version, endpoint)) return null;
        }
        var account = await gateway.GetCurrentUserAsync(endpoint, tokens.AccessToken, ct);
        if (account.Id == Guid.Empty || string.IsNullOrWhiteSpace(account.Email) || (expectedId.HasValue && account.Id != expectedId))
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "The saved account could not be verified.");
            return null;
        }
        var expiry = start.AddSeconds(tokens.ExpiresIn) - TimeSpan.FromSeconds(Math.Min(30, tokens.ExpiresIn / 10.0));
        if (clock.GetUtcNow() >= expiry) throw new InvalidOperationException("Expired server response.");
        await storageGate.WaitAsync(ct);
        try
        {
            lock (sync)
            {
                if (!Current(version, endpoint)) return null;
            }
            try
            {
                await store.WriteAsync(JsonSerializer.Serialize(new Credential(1, endpoint.AbsoluteUri, account.Id, tokens.RefreshToken)), ct);
                ct.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The adapter may have committed before observing cancellation. No newer
                // write can enter while the storage gate is held.
                await ClearInterruptedWriteAsync();
                throw;
            }
            catch
            {
                Fail(version, endpoint, AuthStatus.StorageError, "Unable to save sign-in securely.");
                return null;
            }

            lock (sync)
            {
                if (Current(version, endpoint))
                {
                    context = new(endpoint, account.Id, tokens.AccessToken);
                    refreshToken = tokens.RefreshToken;
                    expiresAt = expiry;
                    Publish(new(AuthStatus.SignedIn, account));
                    return context;
                }
            }

            // This write belongs to an invalidated operation. The gate excludes newer
            // writers until its value has been removed.
            await ClearInterruptedWriteAsync();
            return null;
        }
        finally
        {
            storageGate.Release();
        }
    }

    private async Task ClearInterruptedWriteAsync()
    {
        try { await store.ClearAsync(CancellationToken.None); }
        catch
        {
            throw new CredentialCleanupException();
        }
    }

    private sealed class CredentialCleanupException : Exception;

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var version = Begin(AuthStatus.SigningOut);
        try
        {
            await storageGate.WaitAsync(cancellationToken);
            try
            {
                lock (sync)
                {
                    if (generation != version) return;
                }
                await store.ClearAsync(cancellationToken);
            }
            finally
            {
                storageGate.Release();
            }
            Fail(version, null, AuthStatus.SignedOut, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(version, null, AuthStatus.StorageError, "Unable to clear saved sign-in.");
            throw;
        }
        catch
        {
            Fail(version, null, AuthStatus.StorageError, "Unable to clear saved sign-in.");
        }
    }

    public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (context is null) return Task.FromResult<AuthContext?>(null);
            if (Endpoint() != context.BaseUri)
            {
                generation++;
                context = null;
                refreshToken = null;
                refreshTask = null;
                Publish(new(AuthStatus.RequiresSignIn, null, "The server changed. Please sign in again."));
                return Task.FromResult<AuthContext?>(null);
            }
            if (clock.GetUtcNow() < expiresAt) return Task.FromResult<AuthContext?>(context);
            return RefreshLocked(context, cancellationToken);
        }
    }
    public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync) return RefreshLocked(rejected, cancellationToken);
    }
    private Task<AuthContext?> RefreshLocked(AuthContext rejected, CancellationToken ct)
    {
        if (context is null) return Task.FromResult<AuthContext?>(null);
        if (!Current(generation, context.BaseUri) || rejected.BaseUri != context.BaseUri || rejected.AccountId != context.AccountId) return Task.FromResult<AuthContext?>(null);
        if (rejected.AccessToken != context.AccessToken) return Task.FromResult<AuthContext?>(context);
        if (refreshTask is null || refreshTask.IsCompleted) refreshTask = RefreshCoreAsync(generation, context.BaseUri, context.AccountId, refreshToken!);
        return refreshTask.WaitAsync(ct);
    }
    private async Task<AuthContext?> RefreshCoreAsync(long version, Uri endpoint, Guid accountId, string token)
    {
        // Yield so the shared task is installed before any synchronous fake or gateway completes.
        await Task.Yield();
        try
        {
            lock (sync)
            {
                if (!Current(version, endpoint)) return null;
            }
            var start = clock.GetUtcNow();
            var tokens = await gateway.RefreshAsync(endpoint, token, CancellationToken.None);
            return await CompleteAsync(version, endpoint, tokens, accountId, start, CancellationToken.None);
        }
        catch (CredentialCleanupException)
        {
            Fail(version, endpoint, AuthStatus.StorageError, "Unable to clear saved sign-in.");
            return null;
        }
        catch (ApiException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Fail(version, endpoint, AuthStatus.RequiresSignIn, "Please sign in again.");
            return null;
        }
        catch
        {
            // The saved credential remains available for a later RestoreAsync retry.
            Fail(version, endpoint, AuthStatus.ConnectionError, "Unable to refresh sign-in. Try again when connected.");
            return null;
        }
    }
    private sealed record Credential(int Version, string Server, Guid AccountId, string RefreshToken)
    {
        public override string ToString() => "Credential { [redacted] }";
    }
}
