using MuscleForge.Mobile.Core.Workspaces;

namespace MuscleForge.Mobile.Services;

public sealed class WorkspacePreferencesStore : IWorkspaceStore
{
    private const string Key = "verified_local_workspace";

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Preferences.Default.Get(Key, string.Empty);
        return Task.FromResult<string?>(value.Length == 0 ? null : value);
    }

    public Task WriteAsync(string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(Key, value);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
