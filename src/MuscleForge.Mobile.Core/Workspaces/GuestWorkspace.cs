using System.Text.Json;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Core.Workspaces;

/// <summary>Persists a non-secret device workspace ID independently of account and credential state.</summary>
public interface IGuestWorkspaceStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(string value, CancellationToken cancellationToken = default);
}

public interface IGuestWorkspace
{
    TemplateScope? CurrentScope { get; }
    bool IsReady { get; }
    string? Error { get; }
    string? Notice { get; }
    event EventHandler? Changed;
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Initializes the local workspace without authentication or network access.</summary>
public sealed class GuestWorkspace(
    IGuestWorkspaceStore store,
    IWorkspaceStore legacyStore,
    IConnectionSettings settings,
    ILegacyTemplateMigration migration) : IGuestWorkspace
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private TemplateScope? scope;
    private string? error;
    private string? notice;
    public TemplateScope? CurrentScope { get { lock (sync) return scope; } }
    public bool IsReady { get { lock (sync) return scope is not null; } }
    public string? Error { get { lock (sync) return error; } }
    public string? Notice { get { lock (sync) return notice; } }
    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            lock (sync) { if (scope is not null) return; }
            try
            {
                var value = await store.ReadAsync(cancellationToken);
                Guid id;
                if (value is null)
                {
                    id = Guid.NewGuid();
                    await store.WriteAsync(id.ToString("D"), cancellationToken);
                }
                else if (!Guid.TryParseExact(value, "D", out id) || id == Guid.Empty)
                    throw new InvalidDataException("The saved local workspace ID is invalid.");

                var guest = TemplateScope.CreateGuest(id);
                var legacyValue = await legacyStore.ReadAsync(cancellationToken);
                var source = ReadRememberedScope(legacyValue, settings.ApiBaseUrl);
                var result = source is not null
                    ? await migration.ImportLibraryAsync(source, guest, cancellationToken)
                    : null;
                cancellationToken.ThrowIfCancellationRequested();
                lock (sync)
                {
                    scope = guest;
                    error = null;
                    notice = result is { Conflicts: > 0 }
                        ? "Some saved templates could not be imported because IDs conflict. Your original library is unchanged; review Existing libraries in Settings."
                        : null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                lock (sync) error = "Could not open the local workspace. Please try again.";
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally { gate.Release(); }
    }

    private static TemplateScope? ReadRememberedScope(string? value, string? configuredServer)
    {
        try
        {
            var saved = value is { Length: <= 4096 } ? JsonSerializer.Deserialize<WorkspaceRecord>(value) : null;
            if (saved is not { Version: 1 } || !ApiEndpoint.TryParse(configuredServer, out var endpoint)) return null;
            var source = TemplateScope.Create(saved.Server, saved.AccountId);
            return source.Server == endpoint ? source : null;
        }
        catch (Exception e) when (e is JsonException or TemplateValidationException or ArgumentNullException) { return null; }
    }

    private sealed record WorkspaceRecord(int Version, string Server, Guid AccountId);
}
