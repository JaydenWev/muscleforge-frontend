using System.Diagnostics.CodeAnalysis;
using MuscleForge.Mobile.Core.Templates;
using SQLite;

namespace MuscleForge.Mobile.Storage;

/// <summary>Stores account-scoped template graphs in the app's local SQLite database.</summary>
public sealed partial class SqliteLocalTemplateStore(string databasePath) : ILocalTemplateStore, MuscleForge.Mobile.Core.Workspaces.ILegacyTemplateMigration
{
    private readonly string path = string.IsNullOrWhiteSpace(databasePath) ? throw new ArgumentException("A database path is required.", nameof(databasePath)) : databasePath;
    private const string SchemaV1 = """
        CREATE TABLE local_templates (
            server TEXT NOT NULL,
            account_id TEXT NOT NULL,
            id TEXT NOT NULL,
            name TEXT NOT NULL,
            archived_at TEXT NULL,
            PRIMARY KEY(server, account_id, id)
        );
        CREATE TABLE local_template_exercises (
            server TEXT NOT NULL,
            account_id TEXT NOT NULL,
            template_id TEXT NOT NULL,
            id TEXT NOT NULL,
            position INTEGER NOT NULL CHECK(typeof(position) = 'integer' AND position BETWEEN 0 AND 99),
            exercise_id TEXT NOT NULL,
            name TEXT NOT NULL,
            equipment TEXT NOT NULL,
            tracking INTEGER NOT NULL CHECK(typeof(tracking) = 'integer' AND tracking = 0),
            load_convention INTEGER NOT NULL CHECK(typeof(load_convention) = 'integer' AND load_convention IN (0, 1)),
            is_archived INTEGER NOT NULL CHECK(typeof(is_archived) = 'integer' AND is_archived IN (0, 1)),
            rest_seconds INTEGER NULL CHECK(rest_seconds IS NULL OR (typeof(rest_seconds) = 'integer' AND rest_seconds BETWEEN 0 AND 86400)),
            PRIMARY KEY(server, account_id, template_id, id),
            UNIQUE(server, account_id, template_id, position),
            FOREIGN KEY(server, account_id, template_id) REFERENCES local_templates(server, account_id, id) ON DELETE CASCADE
        );
        CREATE TABLE local_template_sets (
            server TEXT NOT NULL,
            account_id TEXT NOT NULL,
            template_id TEXT NOT NULL,
            template_exercise_id TEXT NOT NULL,
            id TEXT NOT NULL,
            position INTEGER NOT NULL CHECK(typeof(position) = 'integer' AND position BETWEEN 0 AND 99),
            kind INTEGER NOT NULL CHECK(typeof(kind) = 'integer' AND kind IN (0, 1)),
            rep_min INTEGER NULL,
            rep_max INTEGER NULL,
            weight_thousandths INTEGER NULL CHECK(weight_thousandths IS NULL OR (typeof(weight_thousandths) = 'integer' AND weight_thousandths BETWEEN 0 AND 9999999999)),
            CHECK((rep_min IS NULL AND rep_max IS NULL) OR
                  (typeof(rep_min) = 'integer' AND typeof(rep_max) = 'integer' AND rep_min BETWEEN 1 AND 2147483647 AND rep_max BETWEEN rep_min AND 2147483647)),
            PRIMARY KEY(server, account_id, template_id, template_exercise_id, id),
            UNIQUE(server, account_id, template_id, id),
            UNIQUE(server, account_id, template_id, template_exercise_id, position),
            FOREIGN KEY(server, account_id, template_id, template_exercise_id) REFERENCES local_template_exercises(server, account_id, template_id, id) ON DELETE CASCADE
        );
        """;

    private SQLiteConnection Open()
    {
        var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
        try
        {
            db.BusyTimeout = TimeSpan.FromSeconds(10);
            db.Execute("PRAGMA foreign_keys = ON");
            db.Execute("BEGIN IMMEDIATE");
            try
            {
                var version = db.ExecuteScalar<int>("PRAGMA user_version");
                if (version > 3) throw new TemplateStorageException("The local database was created by a newer app version.");
                if (version == 0)
                {
                    foreach (var statement in SchemaV1.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) db.Execute(statement);
                    db.Execute("PRAGMA user_version = 1");
                    version = 1;
                }
                if (version == 1)
                {
                    db.Execute("ALTER TABLE local_templates ADD COLUMN revision INTEGER NOT NULL DEFAULT 1 CHECK(typeof(revision) = 'integer' AND revision > 0)");
                    db.Execute("CREATE INDEX local_templates_listing ON local_templates(server,account_id,archived_at,name,id)");
                    db.Execute("PRAGMA user_version = 2");
                    version = 2;
                }
                if (version == 2)
                {
                    db.Execute("CREATE TABLE local_template_import_receipts (source_server TEXT NOT NULL, source_account_id TEXT NOT NULL, template_id TEXT NOT NULL, guest_server TEXT NOT NULL, guest_account_id TEXT NOT NULL, PRIMARY KEY(source_server,source_account_id,template_id,guest_server,guest_account_id))");
                    db.Execute("PRAGMA user_version = 3");
                }
                db.Execute("COMMIT");
            }
            catch
            {
                db.Execute("ROLLBACK");
                throw;
            }
            return db;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    private static string Server(TemplateScope scope) => scope.Server.AbsoluteUri;
    private static string Id(Guid id) => id.ToString("D");
    private static string? Timestamp(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    private static DateTimeOffset? Timestamp(string? value) => value is null ? null : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private async Task<T> Run<T>(Func<SQLiteConnection, T> action, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var db = Open();
                cancellationToken.ThrowIfCancellationRequested();
                return action(db);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (TemplateValidationException) { throw; }
        catch (TemplateConflictException) { throw; }
        catch (TemplateNotFoundException) { throw; }
        catch (TemplateStorageException) { throw; }
        catch (Exception ex) { throw new TemplateStorageException("Unable to access local templates.", ex); }
    }

    public Task<LocalTemplate?> GetAsync(TemplateScope scope, Guid id, CancellationToken cancellationToken = default) =>
        Run(db =>
        {
            db.Execute("BEGIN");
            try
            {
                var result = Read(db, scope, id);
                cancellationToken.ThrowIfCancellationRequested();
                db.Execute("COMMIT");
                return result;
            }
            catch
            {
                db.Execute("ROLLBACK");
                throw;
            }
        }, cancellationToken);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TemplateRow))]
    public Task<IReadOnlyList<LocalTemplateSummary>> ListAsync(TemplateScope scope, int offset, int limit, bool includeArchived, CancellationToken cancellationToken = default)
    {
        LocalTemplateValidation.ValidatePage(offset, limit);
        return Run<IReadOnlyList<LocalTemplateSummary>>(db => db.Query<TemplateRow>("SELECT id, name, revision, archived_at AS ArchivedAt FROM local_templates WHERE server = ? AND account_id = ? AND (? = 1 OR archived_at IS NULL) ORDER BY name COLLATE NOCASE, id LIMIT ? OFFSET ?", Server(scope), Id(scope.AccountId), includeArchived ? 1 : 0, limit, offset)
            .Select(row => new LocalTemplateSummary(Guid.Parse(row.Id), row.Name, row.Revision, Timestamp(row.ArchivedAt))).ToArray(), cancellationToken);
    }

    public Task<LocalTemplate> SaveAsync(TemplateScope scope, LocalTemplate template, long expectedRevision, CancellationToken cancellationToken = default) => Run(db =>
    {
        db.Execute("BEGIN IMMEDIATE");
        try
        {
            var existing = Read(db, scope, template.Id);
            if (expectedRevision != template.Revision || (existing is null ? expectedRevision != 0 : existing.Revision != expectedRevision)) throw new TemplateConflictException("The local template has changed.");
            LocalTemplateValidation.ValidateForSave(scope, template, existing);
            cancellationToken.ThrowIfCancellationRequested();
            var server = Server(scope);
            var account = Id(scope.AccountId);
            var templateId = Id(template.Id);
            var next = checked(expectedRevision + 1);
            if (existing is null)
                db.Execute("INSERT INTO local_templates(server,account_id,id,name,archived_at,revision) VALUES(?,?,?,?,?,?)", server, account, templateId, template.Name, Timestamp(template.ArchivedAt), next);
            else
            {
                if (db.Execute("UPDATE local_templates SET name=?, archived_at=?, revision=? WHERE server=? AND account_id=? AND id=? AND revision=?", template.Name, Timestamp(template.ArchivedAt), next, server, account, templateId, expectedRevision) != 1) throw new TemplateConflictException("The local template has changed.");
                db.Execute("DELETE FROM local_template_exercises WHERE server=? AND account_id=? AND template_id=?", server, account, templateId);
            }
            foreach (var exercise in template.Exercises)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exerciseId = Id(exercise.Id);
                var metadata = exercise.Metadata;
                db.Execute("INSERT INTO local_template_exercises(server,account_id,template_id,id,position,exercise_id,name,equipment,tracking,load_convention,is_archived,rest_seconds) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", server, account, templateId, exerciseId, exercise.Position, Id(metadata.ExerciseId), metadata.Name, metadata.Equipment, (int)metadata.Tracking, (int)metadata.LoadConvention, metadata.IsArchived ? 1 : 0, exercise.RestSeconds);
                foreach (var set in exercise.Sets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    db.Execute("INSERT INTO local_template_sets(server,account_id,template_id,template_exercise_id,id,position,kind,rep_min,rep_max,weight_thousandths) VALUES(?,?,?,?,?,?,?,?,?,?)", server, account, templateId, exerciseId, Id(set.Id), set.Position, (int)set.Kind, set.RepMin, set.RepMax, set.WeightKg is null ? null : (long?)(set.WeightKg.Value * 1000m));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            var saved = Read(db, scope, template.Id)!;
            db.Execute("COMMIT");
            return saved;
        }
        catch
        {
            db.Execute("ROLLBACK");
            throw;
        }
    }, cancellationToken);

    public Task<LocalTemplate> ArchiveAsync(TemplateScope scope, Guid id, long expectedRevision, CancellationToken cancellationToken = default) => Run(db =>
    {
        db.Execute("BEGIN IMMEDIATE");
        try
        {
            var existing = Read(db, scope, id) ?? throw new TemplateNotFoundException("The local template was not found.");
            if (expectedRevision <= 0 || existing.Revision != expectedRevision || existing.ArchivedAt is not null) throw new TemplateConflictException("The local template has changed or is archived.");
            cancellationToken.ThrowIfCancellationRequested();
            if (db.Execute("UPDATE local_templates SET archived_at=?, revision=? WHERE server=? AND account_id=? AND id=? AND revision=?", Timestamp(DateTimeOffset.UtcNow), checked(expectedRevision + 1), Server(scope), Id(scope.AccountId), Id(id), expectedRevision) != 1) throw new TemplateConflictException("The local template has changed.");
            var saved = Read(db, scope, id)!;
            cancellationToken.ThrowIfCancellationRequested();
            db.Execute("COMMIT");
            return saved;
        }
        catch
        {
            db.Execute("ROLLBACK");
            throw;
        }
    }, cancellationToken);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TemplateRow))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExerciseRow))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SetRow))]
    private static LocalTemplate? Read(SQLiteConnection db, TemplateScope scope, Guid id)
    {
        var server = Server(scope);
        var account = Id(scope.AccountId);
        var templateId = Id(id);
        var row = db.Query<TemplateRow>("SELECT id,name,revision,archived_at AS ArchivedAt FROM local_templates WHERE server=? AND account_id=? AND id=?", server, account, templateId).SingleOrDefault();
        if (row is null) return null;
        var exercises = db.Query<ExerciseRow>("SELECT id,position,exercise_id AS ExerciseId,name,equipment,tracking,load_convention AS LoadConvention,is_archived AS IsArchived,rest_seconds AS RestSeconds FROM local_template_exercises WHERE server=? AND account_id=? AND template_id=? ORDER BY position", server, account, templateId);
        var sets = db.Query<SetRow>("SELECT template_exercise_id AS TemplateExerciseId,id,position,kind,rep_min AS RepMin,rep_max AS RepMax,weight_thousandths AS WeightThousandths FROM local_template_sets WHERE server=? AND account_id=? AND template_id=? ORDER BY position", server, account, templateId);
        return new LocalTemplate(scope, id, row.Name, row.Revision, Timestamp(row.ArchivedAt), exercises.Select(e => new LocalTemplateExercise(Guid.Parse(e.Id), e.Position,
            new LocalExerciseMetadata(scope, Guid.Parse(e.ExerciseId), e.Name, e.Equipment, (ExerciseTrackingType)e.Tracking, (MuscleForge.Mobile.Core.Catalog.ExerciseLoadConvention)e.LoadConvention, e.IsArchived != 0), e.RestSeconds,
            sets.Where(s => s.TemplateExerciseId == e.Id).Select(s => new LocalTemplateSet(Guid.Parse(s.Id), s.Position, (TemplateSetKind)s.Kind, s.RepMin, s.RepMax, s.WeightThousandths / 1000m)))));
    }

    private sealed class TemplateRow
    {
        public TemplateRow() { }
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public long Revision { get; set; }
        public string? ArchivedAt { get; set; }
    }

    private sealed class ExerciseRow
    {
        public ExerciseRow() { }
        public string Id { get; set; } = "";
        public int Position { get; set; }
        public string ExerciseId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Equipment { get; set; } = "";
        public int Tracking { get; set; }
        public int LoadConvention { get; set; }
        public int IsArchived { get; set; }
        public int? RestSeconds { get; set; }
    }

    private sealed class SetRow
    {
        public SetRow() { }
        public string TemplateExerciseId { get; set; } = "";
        public string Id { get; set; } = "";
        public int Position { get; set; }
        public int Kind { get; set; }
        public int? RepMin { get; set; }
        public int? RepMax { get; set; }
        public long? WeightThousandths { get; set; }
    }
}
