using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Core.Workspaces;

/// <summary>A legacy account partition available for explicit import.</summary>
public sealed record LegacyTemplateLibrary(TemplateScope Scope, int TemplateCount);

/// <summary>Counts of source templates copied, previously copied, or left untouched due to a conflict.</summary>
public sealed record LegacyTemplateImportResult(int Imported, int AlreadyImported, int Conflicts);

/// <summary>Copies libraries without removing or modifying the source partition. Imports must be retry-safe.</summary>
public interface ILegacyTemplateMigration
{
    Task<IReadOnlyList<LegacyTemplateLibrary>> ListLibrariesAsync(CancellationToken cancellationToken = default);
    Task<LegacyTemplateImportResult> ImportLibraryAsync(TemplateScope sourceScope, TemplateScope guestScope, CancellationToken cancellationToken = default);
}
