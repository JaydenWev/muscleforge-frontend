using System.Text.Json;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Core.Workspaces;

// This non-secret record enables local access only. It must never be used as API authorization.
public interface IWorkspaceStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(string value, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ILocalWorkspace
{
    TemplateScope? CurrentScope { get; }
    bool IsExplicitlySignedOut { get; }
    void Suspend();
    event EventHandler? Changed;
    Task RestoreAsync(CancellationToken cancellationToken = default);
    Task RememberVerifiedAsync(TemplateScope scope, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalWorkspace(IWorkspaceStore store, IConnectionSettings settings) : ILocalWorkspace
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private TemplateScope? scope;
    private long generation;
    private bool signedOut;
    public bool IsExplicitlySignedOut { get { lock (sync) return signedOut; } }
    public event EventHandler? Changed;

    public TemplateScope? CurrentScope
    {
        get
        {
            lock (sync)
                return scope is not null && ApiEndpoint.TryParse(settings.ApiBaseUrl, out var endpoint) && endpoint == scope.Server ? scope : null;
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        long version;
        lock (sync) version = generation;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var value = await store.ReadAsync(cancellationToken);
            TemplateScope? restored = null;
            var restoredSignedOut = false;
            try
            {
                var saved = value is { Length: <= 4096 } ? JsonSerializer.Deserialize<WorkspaceRecord>(value) : null;
                if (saved is { Version: 2 }) restoredSignedOut = true;
                else if (saved is { Version: 1 }) restored = TemplateScope.Create(saved.Server, saved.AccountId);
            }
            catch (Exception e) when (e is JsonException or TemplateValidationException or ArgumentNullException) { }
            lock (sync)
            {
                if (version != generation) return;
                scope = restored;
                signedOut = restoredSignedOut;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally { gate.Release(); }
    }

    public async Task RememberVerifiedAsync(TemplateScope verifiedScope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verifiedScope);
        long version;
        lock (sync) version = generation;
        await gate.WaitAsync(cancellationToken);
        try
        {
            lock (sync) { if (version != generation) return; }
            await store.WriteAsync(JsonSerializer.Serialize(new WorkspaceRecord(1, verifiedScope.Server.AbsoluteUri, verifiedScope.AccountId)), cancellationToken);
            lock (sync)
            {
                if (version != generation) return;
                generation++; scope = verifiedScope; signedOut = false;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally { gate.Release(); }
    }

    public void Suspend()
    {
        lock (sync) { generation++; scope = null; signedOut = true; }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Suspend();
        await gate.WaitAsync(cancellationToken);
        try
        {
            await store.WriteAsync(JsonSerializer.Serialize(new WorkspaceRecord(2, string.Empty, Guid.Empty)), cancellationToken);
        }
        finally { gate.Release(); }
    }

    private sealed record WorkspaceRecord(int Version, string Server, Guid AccountId);
}
