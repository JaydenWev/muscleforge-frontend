using System.Diagnostics.CodeAnalysis;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Core.Workspaces;

namespace MuscleForge.Mobile.Storage;

public sealed partial class SqliteLocalTemplateStore
{
    // Limits keep malformed or unexpectedly large databases from materializing unbounded lists.
    private const int MaxLegacyLibraries = 1000;
    private const int MaxLibraryTemplates = 10000;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LibraryRow))]
    public Task<IReadOnlyList<LegacyTemplateLibrary>> ListLibrariesAsync(CancellationToken cancellationToken = default) =>
        Run<IReadOnlyList<LegacyTemplateLibrary>>(db =>
        {
            var rows = db.Query<LibraryRow>("SELECT server AS Server, account_id AS AccountId, COUNT(*) AS TemplateCount FROM local_templates WHERE server LIKE 'https://%' GROUP BY server, account_id ORDER BY server, account_id LIMIT ?", MaxLegacyLibraries + 1);
            if (rows.Count > MaxLegacyLibraries) throw new TemplateStorageException("There are too many legacy template libraries to list safely.");
            cancellationToken.ThrowIfCancellationRequested();
            return rows.Select(row => new LegacyTemplateLibrary(TemplateScope.Create(row.Server, Guid.Parse(row.AccountId)), row.TemplateCount)).ToArray();
        }, cancellationToken);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ImportIdRow))]
    public Task<LegacyTemplateImportResult> ImportLibraryAsync(TemplateScope sourceScope, TemplateScope guestScope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceScope);
        ArgumentNullException.ThrowIfNull(guestScope);
        if (sourceScope.IsGuest || !guestScope.IsGuest)
            throw new TemplateValidationException("A legacy account source and guest destination are required.");
        return Run(db =>
        {
            db.Execute("BEGIN IMMEDIATE");
            try
            {
                var sourceServer = Server(sourceScope); var sourceAccount = Id(sourceScope.AccountId);
                var guestServer = Server(guestScope); var guestAccount = Id(guestScope.AccountId);
                var ids = db.Query<ImportIdRow>("SELECT id AS Id FROM local_templates WHERE server=? AND account_id=? ORDER BY id LIMIT ?", sourceServer, sourceAccount, MaxLibraryTemplates + 1);
                if (ids.Count > MaxLibraryTemplates) throw new TemplateStorageException("The legacy template library is too large to import safely.");
                var imported = 0; var alreadyImported = 0; var conflicts = 0;
                foreach (var row in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM local_template_import_receipts WHERE source_server=? AND source_account_id=? AND template_id=? AND guest_server=? AND guest_account_id=?", sourceServer, sourceAccount, row.Id, guestServer, guestAccount) != 0)
                    { alreadyImported++; continue; }
                    if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM local_templates WHERE server=? AND account_id=? AND id=?", guestServer, guestAccount, row.Id) != 0)
                    { conflicts++; continue; }
                    db.Execute("INSERT INTO local_templates(server,account_id,id,name,archived_at,revision) SELECT ?,?,id,name,archived_at,revision FROM local_templates WHERE server=? AND account_id=? AND id=?", guestServer, guestAccount, sourceServer, sourceAccount, row.Id);
                    db.Execute("INSERT INTO local_template_exercises(server,account_id,template_id,id,position,exercise_id,name,equipment,tracking,load_convention,is_archived,rest_seconds) SELECT ?,?,template_id,id,position,exercise_id,name,equipment,tracking,load_convention,is_archived,rest_seconds FROM local_template_exercises WHERE server=? AND account_id=? AND template_id=?", guestServer, guestAccount, sourceServer, sourceAccount, row.Id);
                    db.Execute("INSERT INTO local_template_sets(server,account_id,template_id,template_exercise_id,id,position,kind,rep_min,rep_max,weight_thousandths) SELECT ?,?,template_id,template_exercise_id,id,position,kind,rep_min,rep_max,weight_thousandths FROM local_template_sets WHERE server=? AND account_id=? AND template_id=?", guestServer, guestAccount, sourceServer, sourceAccount, row.Id);
                    db.Execute("INSERT INTO local_template_import_receipts VALUES(?,?,?,?,?)", sourceServer, sourceAccount, row.Id, guestServer, guestAccount);
                    imported++;
                }
                cancellationToken.ThrowIfCancellationRequested();
                db.Execute("COMMIT");
                return new LegacyTemplateImportResult(imported, alreadyImported, conflicts);
            }
            catch { db.Execute("ROLLBACK"); throw; }
        }, cancellationToken);
    }

    private sealed class LibraryRow
    {
        public LibraryRow() { }
        public string Server { get; set; } = "";
        public string AccountId { get; set; } = "";
        public int TemplateCount { get; set; }
    }

    private sealed class ImportIdRow
    {
        public ImportIdRow() { }
        public string Id { get; set; } = "";
    }
}
