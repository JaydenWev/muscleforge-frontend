using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Storage;
using SQLite;
using Xunit;

namespace MuscleForge.Mobile.StorageTests;

public sealed class SqliteLegacyTemplateMigrationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "muscleforge-import-tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "templates.db");
    private static TemplateScope Source => TemplateScope.Create("https://example.test/", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static TemplateScope Guest => TemplateScope.CreateGuest(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    public SqliteLegacyTemplateMigrationTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task PopulatedV2UpgradeCopiesExactGraphAndRepeatPreservesGuestEdits()
    {
        var store = new SqliteLocalTemplateStore(Database);
        var original = await store.SaveAsync(Source, Template(Source), 0, TestContext.Current.CancellationToken);
        var archived = await store.ArchiveAsync(Source, original.Id, original.Revision, TestContext.Current.CancellationToken);
        using (var db = new SQLiteConnection(Database))
        {
            db.Execute("DROP TABLE local_template_import_receipts");
            db.Execute("PRAGMA user_version=2");
        }
        var libraries = await store.ListLibrariesAsync(TestContext.Current.CancellationToken);
        Assert.Single(libraries);
        Assert.Equal(Source, libraries[0].Scope);
        Assert.Equal(1, libraries[0].TemplateCount);
        var result = await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Imported);
        var copy = await store.GetAsync(Guest, original.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(copy);
        Assert.Equal(archived.Revision, copy.Revision);
        Assert.Equal(archived.ArchivedAt, copy.ArchivedAt);
        Assert.Equal(Guest, copy.Exercises[0].Metadata.Scope);
        Assert.Equal(archived.Exercises[0].Id, copy.Exercises[0].Id);
        Assert.Equal(archived.Exercises[0].Metadata.ExerciseId, copy.Exercises[0].Metadata.ExerciseId);
        Assert.Equal(archived.Exercises[0].RestSeconds, copy.Exercises[0].RestSeconds);
        Assert.Equal(archived.Exercises[0].Sets[0], copy.Exercises[0].Sets[0]);
        Assert.Equal(archived.Name, (await store.GetAsync(Source, original.Id, TestContext.Current.CancellationToken))!.Name);
        using var db2 = new SQLiteConnection(Database);
        Assert.Equal(3, db2.ExecuteScalar<int>("PRAGMA user_version"));
        db2.Execute("UPDATE local_templates SET name='Guest edit' WHERE server=? AND account_id=? AND id=?", Guest.Server.AbsoluteUri, Guest.AccountId.ToString("D"), original.Id.ToString("D"));
        var repeat = await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken);
        Assert.Equal(1, repeat.AlreadyImported);
        Assert.Equal("Guest edit", (await store.GetAsync(Guest, original.Id, TestContext.Current.CancellationToken))!.Name);
        Assert.Single(await store.ListLibrariesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RepeatKeepsGuestEditsAndDoesNotReimportAfterDeletion()
    {
        var store = new SqliteLocalTemplateStore(Database);
        var source = await store.SaveAsync(Source, Template(Source), 0, TestContext.Current.CancellationToken);
        await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken);
        var guest = (await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken))!;
        var edit = new LocalTemplate(Guest, guest.Id, "Guest edit", guest.Revision, null, guest.Exercises);
        await store.SaveAsync(Guest, edit, guest.Revision, TestContext.Current.CancellationToken);
        var sourceEdit = new LocalTemplate(Source, source.Id, "Source edit", source.Revision, null, source.Exercises);
        await store.SaveAsync(Source, sourceEdit, source.Revision, TestContext.Current.CancellationToken);
        Assert.Equal(1, (await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken)).AlreadyImported);
        Assert.Equal("Guest edit", (await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken))!.Name);
        using (var db = new SQLiteConnection(Database))
        {
            db.Execute("PRAGMA foreign_keys=ON");
            db.Execute("DELETE FROM local_templates WHERE server=? AND account_id=? AND id=?", Guest.Server.AbsoluteUri, Guest.AccountId.ToString("D"), source.Id.ToString("D"));
        }
        Assert.Equal(1, (await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken)).AlreadyImported);
        Assert.Null(await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CollisionIsReportedAndNeitherSourceNorDestinationChanges()
    {
        var store = new SqliteLocalTemplateStore(Database);
        var source = await store.SaveAsync(Source, Template(Source), 0, TestContext.Current.CancellationToken);
        var target = new LocalTemplate(Guest, source.Id, "Existing", 0, null, []);
        await store.SaveAsync(Guest, target, 0, TestContext.Current.CancellationToken);
        var result = await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal(0, result.Imported);
        Assert.Equal("Existing", (await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken))!.Name);
        Assert.Equal(source.Name, (await store.GetAsync(Source, source.Id, TestContext.Current.CancellationToken))!.Name);
        await Assert.ThrowsAsync<TemplateValidationException>(() => store.ImportLibraryAsync(Guest, Source, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SqlFailureAndCancellationLeaveNoPartialImportOrReceipts()
    {
        var store = new SqliteLocalTemplateStore(Database);
        var source = await store.SaveAsync(Source, Template(Source), 0, TestContext.Current.CancellationToken);
        using (var db = new SQLiteConnection(Database))
            db.Execute("CREATE TRIGGER reject_import BEFORE INSERT ON local_template_sets WHEN NEW.server='local://device/' BEGIN SELECT RAISE(ABORT, 'synthetic'); END");
        await Assert.ThrowsAsync<TemplateStorageException>(() => store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken));
        using (var db = new SQLiteConnection(Database))
        {
            Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM local_template_import_receipts"));
            db.Execute("DROP TRIGGER reject_import");
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ImportLibraryAsync(Source, Guest, cancelled.Token));
        Assert.Null(await store.GetAsync(Guest, source.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, (await store.ImportLibraryAsync(Source, Guest, TestContext.Current.CancellationToken)).Imported);
    }

    private static LocalTemplate Template(TemplateScope scope)
    {
        var metadata = new LocalExerciseMetadata(scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, false);
        return new LocalTemplate(scope, Guid.NewGuid(), "Strength", 0, null, [new LocalTemplateExercise(Guid.NewGuid(), 4, metadata, 90, [new LocalTemplateSet(Guid.NewGuid(), 3, TemplateSetKind.Working, 5, 8, 9999999.999m)])]);
    }
    public void Dispose() { try { Directory.Delete(directory, true); } catch (IOException) { } }
}
