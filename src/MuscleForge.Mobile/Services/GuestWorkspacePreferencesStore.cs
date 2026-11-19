using MuscleForge.Mobile.Core.Workspaces;

namespace MuscleForge.Mobile.Services;

public sealed class GuestWorkspacePreferencesStore : IGuestWorkspaceStore
{
    private const string Key = "device_guest_workspace";
    private static Android.Content.ISharedPreferences Open() =>
        Android.App.Application.Context.GetSharedPreferences("muscleforge_local_identity", Android.Content.FileCreationMode.Private)
        ?? throw new IOException("The local identity store is unavailable.");

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.Run<string?>(() =>
    {
        using var preferences = Open();
        // An empty or corrupt saved record must reach validation, not create a new workspace.
        if (!preferences.Contains(Key)) return null;
        var value = preferences.GetString(Key, string.Empty) ?? string.Empty;
        // A failed Android commit can still update its in-memory preference cache.
        // Re-acknowledge a cached identity envelope before a retry can publish its ID.
        if (value.Length > 0)
        {
            using var editor = preferences.Edit() ?? throw new IOException("The local identity store is unavailable.");
            editor.PutString(Key, value);
            if (!editor.Commit()) throw new IOException("The local workspace identity could not be saved.");
        }
        return value;
    }, cancellationToken);

    public Task WriteAsync(string value, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var preferences = Open();
        using var editor = preferences.Edit() ?? throw new IOException("The local identity store is unavailable.");
        editor.PutString(Key, value);
        // Commit acknowledges the disk write. Keep synchronous platform I/O off the UI thread.
        if (!editor.Commit()) throw new IOException("The local workspace identity could not be saved.");
    }, cancellationToken);
}
