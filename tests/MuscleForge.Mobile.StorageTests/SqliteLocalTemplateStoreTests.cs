using System.Text.Json;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Storage;
using Xunit;

namespace MuscleForge.Mobile.StorageTests;

public sealed class SqliteLocalTemplateStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "muscleforge-storage-tests", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "templates.db");
    private static TemplateScope Scope => TemplateScope.Create("https://example.test/api/", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    public SqliteLocalTemplateStoreTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task GraphAndSharedWeightFixturesSurviveReopeningFile()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "units-timezones.json")));
        var weights = fixture.RootElement.GetProperty("weightConversions").EnumerateArray().Select(item =>
        {
            var unit = item.GetProperty("unit").GetString() == "kg" ? WeightUnit.Kg : WeightUnit.Lb;
            Assert.True(Weight.TryToKilograms(item.GetProperty("value").GetDecimal(), unit, out var kg));
            Assert.Equal(item.GetProperty("kilograms").GetDecimal(), kg);
            return kg;
        }).ToArray();
        var scope = Scope;
        var input = MakeTemplate(scope, weights: weights);
        var saved = await new SqliteLocalTemplateStore(Database).SaveAsync(scope, input, 0, TestContext.Current.CancellationToken);
        var actual = await new SqliteLocalTemplateStore(Database).GetAsync(scope, input.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(actual);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(saved.Id, actual.Id);
        Assert.Equal(saved.Name, actual.Name);
        Assert.Equal(saved.Revision, actual.Revision);
        Assert.Equal(input.Exercises.Select(e => e.Id), actual.Exercises.Select(e => e.Id));
        Assert.Equal(input.Exercises[0].Sets.Select(s => s.Id), actual.Exercises[0].Sets.Select(s => s.Id));
        Assert.Equal(weights.Cast<decimal?>(), actual.Exercises[0].Sets.Select(s => s.WeightKg));
        Assert.Null(actual.Exercises[0].RestSeconds);
        Assert.All(actual.Exercises[0].Sets, set => { Assert.Null(set.RepMin); Assert.Null(set.RepMax); });
    }

    [Fact]
    public async Task ScopeSeparatesAccountAndServerAndNormalizesEquivalentUri()
    {
        var original = Scope;
        var equivalent = TemplateScope.Create("https://EXAMPLE.test:443/api", original.AccountId);
        var differentAccount = TemplateScope.Create("https://example.test/api/", Guid.NewGuid());
        var differentServer = TemplateScope.Create("https://other.test/api/", original.AccountId);
        var store = new SqliteLocalTemplateStore(Database);
        var input = MakeTemplate(original);
        await store.SaveAsync(original, input, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(await store.GetAsync(equivalent, input.Id, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(differentAccount, input.Id, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(differentServer, input.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await store.ListAsync(differentAccount, 0, 20, true, TestContext.Current.CancellationToken));
        Assert.Empty(await store.ListAsync(differentServer, 0, 20, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentInstancesOnlyAllowOneCompareAndSwap()
    {
        var scope = Scope;
        var first = new SqliteLocalTemplateStore(Database);
        var second = new SqliteLocalTemplateStore(Database);
        var saved = await first.SaveAsync(scope, MakeTemplate(scope), 0, TestContext.Current.CancellationToken);
        var a = Rename(saved, "First");
        var b = Rename(saved, "Second");
        async Task<bool> Attempt(SqliteLocalTemplateStore store, LocalTemplate candidate)
        {
            try { await store.SaveAsync(scope, candidate, saved.Revision, TestContext.Current.CancellationToken); return true; }
            catch (TemplateConflictException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(() => Attempt(first, a), TestContext.Current.CancellationToken), Task.Run(() => Attempt(second, b), TestContext.Current.CancellationToken));
        Assert.Single(results, result => result);
        var actual = await first.GetAsync(scope, saved.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(actual);
        Assert.Equal(saved.Revision + 1, actual.Revision);
        Assert.Contains(actual.Name, new[] { "First", "Second" });
        Assert.Equal(saved.Exercises[0].Sets.Select(s => s.Id), actual.Exercises[0].Sets.Select(s => s.Id));
    }

    [Fact]
    public async Task ArchiveRetainsGraphAndRejectsStaleWrites()
    {
        var scope = Scope;
        var store = new SqliteLocalTemplateStore(Database);
        var saved = await store.SaveAsync(scope, MakeTemplate(scope), 0, TestContext.Current.CancellationToken);
        var archived = await store.ArchiveAsync(scope, saved.Id, saved.Revision, TestContext.Current.CancellationToken);
        Assert.NotNull(archived.ArchivedAt);
        Assert.Equal(saved.Revision + 1, archived.Revision);
        Assert.Empty(await store.ListAsync(scope, 0, 20, false, TestContext.Current.CancellationToken));
        Assert.Single(await store.ListAsync(scope, 0, 20, true, TestContext.Current.CancellationToken));
        var loaded = await new SqliteLocalTemplateStore(Database).GetAsync(scope, saved.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal(saved.Exercises[0].Sets.Select(s => s.Id), loaded.Exercises[0].Sets.Select(s => s.Id));
        await Assert.ThrowsAsync<TemplateConflictException>(() => store.SaveAsync(scope, Rename(saved, "stale"), saved.Revision, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TemplateConflictException>(() => store.ArchiveAsync(scope, saved.Id, saved.Revision, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchivedExerciseCanBeRetainedButCannotBeNewlySelected()
    {
        var scope = Scope;
        var store = new SqliteLocalTemplateStore(Database);
        var saved = await store.SaveAsync(scope, MakeTemplate(scope), 0, TestContext.Current.CancellationToken);
        var old = saved.Exercises[0];
        var archivedMetadata = new LocalExerciseMetadata(scope, old.Metadata.ExerciseId, old.Metadata.Name, old.Metadata.Equipment, old.Metadata.Tracking, old.Metadata.LoadConvention, true);
        var retained = new LocalTemplateExercise(old.Id, old.Position, archivedMetadata, old.RestSeconds, old.Sets);
        var update = new LocalTemplate(scope, saved.Id, saved.Name, saved.Revision, null, [retained]);
        var updated = await store.SaveAsync(scope, update, saved.Revision, TestContext.Current.CancellationToken);
        Assert.True(updated.Exercises[0].Metadata.IsArchived);
        var newlySelected = new LocalTemplateExercise(Guid.NewGuid(), 0, archivedMetadata, null, old.Sets);
        var invalid = new LocalTemplate(scope, Guid.NewGuid(), "New", 0, null, [newlySelected]);
        await Assert.ThrowsAsync<TemplateValidationException>(() => store.SaveAsync(scope, invalid, 0, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsync(scope, invalid.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InputCollectionsAreCopiedAndPositionsDetermineStoredOrder()
    {
        var scope = Scope;
        var metadata = new LocalExerciseMetadata(scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, false);
        var first = new LocalTemplateSet(Guid.NewGuid(), 0, TemplateSetKind.Warmup, 5, 5, 0m);
        var second = new LocalTemplateSet(Guid.NewGuid(), 1, TemplateSetKind.Working, 5, 8, 20m);
        var sets = new List<LocalTemplateSet> { second, first };
        var exercise = new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, 0, sets);
        var exercises = new List<LocalTemplateExercise> { exercise };
        var input = new LocalTemplate(scope, Guid.NewGuid(), "Strength", 0, null, exercises);
        sets.Clear(); exercises.Clear();
        var store = new SqliteLocalTemplateStore(Database);
        await store.SaveAsync(scope, input, 0, TestContext.Current.CancellationToken);
        var loaded = await store.GetAsync(scope, input.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Single(input.Exercises);
        Assert.Equal(new[] { second.Id, first.Id }, input.Exercises[0].Sets.Select(s => s.Id));
        Assert.Equal(new[] { first.Id, second.Id }, loaded.Exercises[0].Sets.Select(s => s.Id));
        Assert.Equal(0, loaded.Exercises[0].RestSeconds);
    }

    [Fact]
    public async Task NullWeightAndPageBoundsAreHandled()
    {
        var scope = Scope;
        var store = new SqliteLocalTemplateStore(Database);
        var original = MakeTemplate(scope);
        var exercise = original.Exercises[0];
        var unset = new LocalTemplateSet(Guid.NewGuid(), 0, TemplateSetKind.Working, null, null, null);
        var template = new LocalTemplate(scope, original.Id, original.Name, 0, null, [new LocalTemplateExercise(exercise.Id, 0, exercise.Metadata, 86400, [unset])]);
        await store.SaveAsync(scope, template, 0, TestContext.Current.CancellationToken);
        var loaded = await store.GetAsync(scope, template.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Null(loaded.Exercises[0].Sets[0].WeightKg);
        Assert.Equal(86400, loaded.Exercises[0].RestSeconds);
        await Assert.ThrowsAsync<TemplateValidationException>(() => store.ListAsync(scope, -1, 1, false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TemplateValidationException>(() => store.ListAsync(scope, 0, 0, false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TemplateValidationException>(() => store.ListAsync(scope, 0, LocalTemplateValidation.MaximumPageSize + 1, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledOperationsDoNotWrite()
    {
        var scope = Scope;
        var store = new SqliteLocalTemplateStore(Database);
        var template = MakeTemplate(scope);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(scope, template, 0, cancellation.Token));
        Assert.Null(await store.GetAsync(scope, template.Id, TestContext.Current.CancellationToken));
    }

    private static LocalTemplate Rename(LocalTemplate source, string name) =>
        new(source.Scope, source.Id, name, source.Revision, source.ArchivedAt, source.Exercises);

    private static LocalTemplate MakeTemplate(TemplateScope scope, decimal[]? weights = null)
    {
        var metadata = new LocalExerciseMetadata(scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, false);
        var sets = (weights ?? [42.5m]).Select((weight, position) => new LocalTemplateSet(Guid.NewGuid(), position, TemplateSetKind.Working, null, null, weight)).ToArray();
        return new LocalTemplate(scope, Guid.NewGuid(), "Strength", 0, null, [new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, sets)]);
    }

    public void Dispose()
    {
        try { Directory.Delete(directory, true); }
        catch (IOException) { }
    }
}
