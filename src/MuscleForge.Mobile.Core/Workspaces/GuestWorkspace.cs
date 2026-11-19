using System.Text;
using System.Text.Json;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;
using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Core.Workspaces;

/// <summary>Persists the non-secret device workspace identity and its initial import choice independently of credentials.</summary>
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
                TemplateScope? source;
                if (value is null)
                {
                    // Bind at creation, before copying anything. Later account changes cannot change this choice.
                    var legacyValue = await legacyStore.ReadAsync(cancellationToken);
                    source = ReadRememberedScope(legacyValue, settings.ApiBaseUrl);
                    id = Guid.NewGuid();
                    await store.WriteAsync(WriteIdentity(id, source), cancellationToken);
                }
                else
                    (id, source) = ReadIdentity(value);

                var guest = TemplateScope.CreateGuest(id);
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

    // Explicit JSON parsing avoids reflection-based serialization dependencies in trimmed mobile builds.
    private static string WriteIdentity(Guid id, TemplateScope? source)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Version", 1);
            writer.WriteString("WorkspaceId", id.ToString("D"));
            if (source is not null)
            {
                writer.WriteString("LegacyServer", source.Server.AbsoluteUri);
                writer.WriteString("LegacyAccountId", source.AccountId.ToString("D"));
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static (Guid Id, TemplateScope? Source) ReadIdentity(string value)
    {
        if (value.Length > 4096) throw new InvalidDataException("The saved local workspace is invalid.");
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("Version", out var version) || !version.TryGetInt32(out var number) || number != 1 ||
            !root.TryGetProperty("WorkspaceId", out var workspaceId) || workspaceId.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(workspaceId.GetString(), "D", out var id) || id == Guid.Empty)
            throw new InvalidDataException("The saved local workspace is invalid.");
        var hasServer = root.TryGetProperty("LegacyServer", out var server);
        var hasAccount = root.TryGetProperty("LegacyAccountId", out var account);
        if (hasServer != hasAccount) throw new InvalidDataException("The saved local workspace is invalid.");
        if (!hasServer) return (id, null);
        if (server.ValueKind != JsonValueKind.String || account.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(account.GetString(), "D", out var accountId))
            throw new InvalidDataException("The saved local workspace is invalid.");
        return (id, TemplateScope.Create(server.GetString()!, accountId));
    }

    private static TemplateScope? ReadRememberedScope(string? value, string? configuredServer)
    {
        try
        {
            if (value is not { Length: <= 4096 } || !ApiEndpoint.TryParse(configuredServer, out var endpoint)) return null;
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Version", out var version) || !version.TryGetInt32(out var number) || number != 1 ||
                !root.TryGetProperty("Server", out var server) || server.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("AccountId", out var account) || account.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(account.GetString(), out var id)) return null;
            var source = TemplateScope.Create(server.GetString()!, id);
            return source.Server == endpoint ? source : null;
        }
        catch (Exception e) when (e is JsonException or TemplateValidationException or ArgumentException or InvalidOperationException) { return null; }
    }
}
