using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Storage;
using SQLite;
using Xunit;

namespace MuscleForge.Mobile.StorageTests;

public sealed class SqliteSchemaTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "muscleforge-schema-tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "templates.db");
    private static TemplateScope Scope => TemplateScope.Create("https://example.test/", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    public SqliteSchemaTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task TriggerAbortRollsBackRevisionAndEntireChildGraph()
    {
        var scope = Scope;
        var store = new SqliteLocalTemplateStore(Database);
        var saved = await store.SaveAsync(scope, Template(scope), 0, TestContext.Current.CancellationToken);
        using (var db = new SQLiteConnection(Database))
            db.Execute("CREATE TRIGGER reject_sets BEFORE INSERT ON local_template_sets BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END");
        var replacement = new LocalTemplate(scope, saved.Id, "Changed", saved.Revision, null, saved.Exercises);
        await Assert.ThrowsAsync<TemplateStorageException>(() => store.SaveAsync(scope, replacement, saved.Revision, TestContext.Current.CancellationToken));
        var loaded = await new SqliteLocalTemplateStore(Database).GetAsync(scope, saved.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal(saved.Name, loaded.Name);
        Assert.Equal(saved.Revision, loaded.Revision);
        Assert.Equal(saved.Exercises[0].Id, loaded.Exercises[0].Id);
        Assert.Equal(saved.Exercises[0].Sets[0], loaded.Exercises[0].Sets[0]);
    }

    [Fact]
    public async Task PopulatedVersionOneGraphMigratesWithoutLosingIdentityOrValues()
    {
        var scope = Scope;
        var templateId = Guid.NewGuid(); var exerciseId = Guid.NewGuid(); var catalogId = Guid.NewGuid();
        var set1 = Guid.NewGuid(); var set2 = Guid.NewGuid();
        using (var db = new SQLiteConnection(Database))
        {
            var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema-v1.sql"));
            foreach (var statement in schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) db.Execute(statement);
            var server = scope.Server.AbsoluteUri; var account = scope.AccountId.ToString("D");
            db.Execute("INSERT INTO local_templates VALUES(?,?,?,?,?)", server, account, templateId.ToString("D"), "Legacy", null);
            db.Execute("INSERT INTO local_template_exercises VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", server, account, templateId.ToString("D"), exerciseId.ToString("D"), 0, catalogId.ToString("D"), "Squat", "Barbell", 0, 0, 0, 90);
            db.Execute("INSERT INTO local_template_sets VALUES(?,?,?,?,?,?,?,?,?,?)", server, account, templateId.ToString("D"), exerciseId.ToString("D"), set2.ToString("D"), 1, 1, 5, 8, 9999999999L);
            db.Execute("INSERT INTO local_template_sets VALUES(?,?,?,?,?,?,?,?,?,?)", server, account, templateId.ToString("D"), exerciseId.ToString("D"), set1.ToString("D"), 0, 0, null, null, 45359L);
            db.Execute("PRAGMA user_version=1");
        }
        var store = new SqliteLocalTemplateStore(Database);
        var graph = await store.GetAsync(scope, templateId, TestContext.Current.CancellationToken);
        Assert.NotNull(graph);
        Assert.Equal(1, graph.Revision);
        Assert.Equal(scope, graph.Scope);
        Assert.Equal(exerciseId, graph.Exercises[0].Id);
        Assert.Equal(catalogId, graph.Exercises[0].Metadata.ExerciseId);
        Assert.Equal(90, graph.Exercises[0].RestSeconds);
        Assert.Equal(new[] { set1, set2 }, graph.Exercises[0].Sets.Select(s => s.Id));
        Assert.Equal(new decimal?[] { 45.359m, 9999999.999m }, graph.Exercises[0].Sets.Select(s => s.WeightKg));
        using var reopened = new SQLiteConnection(Database);
        Assert.Equal(3, reopened.ExecuteScalar<int>("PRAGMA user_version"));
    }

    [Fact]
    public async Task DatabaseRejectsInvalidNumericValuesAndOneSidedRepRanges()
    {
        var scope = Scope;
        var saved = await new SqliteLocalTemplateStore(Database).SaveAsync(scope, Template(scope), 0, TestContext.Current.CancellationToken);
        var exercise = saved.Exercises[0];
        using var db = new SQLiteConnection(Database);
        var server = scope.Server.AbsoluteUri; var account = scope.AccountId.ToString("D");
        var templateId = saved.Id.ToString("D"); var exerciseId = exercise.Id.ToString("D");
        void RejectSet(object position, object kind, object? min, object? max, object? weight) =>
            Assert.Throws<SQLiteException>(() => db.Execute("INSERT INTO local_template_sets VALUES(?,?,?,?,?,?,?,?,?,?)", server, account, templateId, exerciseId, Guid.NewGuid().ToString("D"), position, kind, min, max, weight));
        RejectSet(1, 1, null, 8, 1000L);
        RejectSet(1, 1, 5, null, 1000L);
        RejectSet(1, 1, 5, 8, 1.5);
        RejectSet(100, 1, 5, 8, 1000L);
        RejectSet(1, 99, 5, 8, 1000L);
        RejectSet(1, 1, 5, 8, 10000000000L);
        Assert.Throws<SQLiteException>(() => db.Execute("UPDATE local_template_exercises SET rest_seconds=86401 WHERE server=? AND account_id=? AND template_id=? AND id=?", server, account, templateId, exerciseId));
        Assert.Throws<SQLiteException>(() => db.Execute("UPDATE local_templates SET revision=1.5 WHERE server=? AND account_id=? AND id=?", server, account, templateId));
        Assert.Equal(1, db.ExecuteScalar<int>("SELECT COUNT(*) FROM local_template_sets"));
    }

    [Fact]
    public async Task FutureSchemaFailsWithoutDroppingData()
    {
        using (var db = new SQLiteConnection(Database))
        {
            db.Execute("CREATE TABLE future_data(value TEXT NOT NULL)");
            db.Execute("INSERT INTO future_data VALUES('retained')");
            db.Execute("PRAGMA user_version=999");
        }
        await Assert.ThrowsAsync<TemplateStorageException>(() => new SqliteLocalTemplateStore(Database).ListAsync(Scope, 0, 10, true, TestContext.Current.CancellationToken));
        using var reopened = new SQLiteConnection(Database);
        Assert.Equal(999, reopened.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal("retained", reopened.ExecuteScalar<string>("SELECT value FROM future_data"));
    }

    [Fact]
    public async Task CompetingInitializationPreservesAllWrites()
    {
        var scope = Scope;
        var templates = Enumerable.Range(0, 8).Select(_ => Template(scope)).ToArray();
        await Task.WhenAll(templates.Select(t => Task.Run(() => new SqliteLocalTemplateStore(Database).SaveAsync(scope, t, 0, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        var listed = await new SqliteLocalTemplateStore(Database).ListAsync(scope, 0, 20, true, TestContext.Current.CancellationToken);
        Assert.Equal(templates.Select(t => t.Id).Order(), listed.Select(t => t.Id).Order());
    }

    [Fact]
    public async Task UnwritablePathSurfacesStorageFailure()
    {
        var store = new SqliteLocalTemplateStore(Path.Combine(directory, "missing-parent", "db.sqlite"));
        await Assert.ThrowsAsync<TemplateStorageException>(() => store.SaveAsync(Scope, Template(Scope), 0, TestContext.Current.CancellationToken));
    }

    private static LocalTemplate Template(TemplateScope scope)
    {
        var metadata = new LocalExerciseMetadata(scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, false);
        return new LocalTemplate(scope, Guid.NewGuid(), "Strength", 0, null, [new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, 60, [new LocalTemplateSet(Guid.NewGuid(), 0, TemplateSetKind.Working, 5, 8, 20m)])]);
    }
    public void Dispose() { try { Directory.Delete(directory, true); } catch (IOException) { } }
}
