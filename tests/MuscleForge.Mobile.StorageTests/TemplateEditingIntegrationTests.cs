using System.Globalization;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Storage;
using Xunit;

namespace MuscleForge.Mobile.StorageTests;

public sealed class TemplateEditingIntegrationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "muscleforge-template-editing", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "templates.db");

    public TemplateEditingIntegrationTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData("nl-NL", "20,5")]
    [InlineData("en-US", "20.5")]
    public async Task OfflineEditingPersistsGraphAndRecoversFromConflictInScopedSqlite(string cultureName, string input)
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = TemplateScope.Create("https://example.test/api/", Guid.NewGuid());
        var store = new SqliteLocalTemplateStore(Database);
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var editor = new TemplateEditorViewModel(store, scope, WeightUnit.Kg, culture);
        await editor.InitializeAsync(ct: ct);
        editor.Name = "Strength";
        var squat = Metadata(scope, "Squat");
        var row = Metadata(scope, "Row");
        Assert.True(editor.AddExercise(squat));
        Assert.True(editor.AddExercise(row));
        var first = editor.Exercises[0];
        first.RestSecondsText = "90";
        Assert.True(first.AddSet());
        first.Sets[0].Kind = TemplateSetKind.Warmup;
        first.Sets[0].RepMinText = "5";
        first.Sets[0].RepMaxText = "8";
        first.Sets[0].WeightText = input;
        Assert.True(editor.SetWeightUnit(WeightUnit.Lb));
        Assert.True(first.AddSet());
        first.Sets[1].Kind = TemplateSetKind.Working;
        first.Sets[1].RepMinText = "3";
        first.Sets[1].RepMaxText = "5";
        first.Sets[1].WeightText = "100";
        first.MoveSet(first.Sets[1], -1);
        editor.MoveExercise(editor.Exercises[1], -1);
        var candidate = editor.CreateSnapshot();
        Assert.True(await editor.SaveAsync(ct));
        Assert.True(editor.SavedOnDevice);
        Assert.False(editor.IsDirty);

        // Construct a fresh adapter and view model over the same file: no network service is involved.
        var reopenedStore = new SqliteLocalTemplateStore(Database);
        var reopened = new TemplateEditorViewModel(reopenedStore, scope, WeightUnit.Kg, culture);
        await reopened.InitializeAsync(candidate.Id, ct);
        var snapshot = reopened.CreateSnapshot();
        Assert.Equal(candidate.Id, snapshot.Id);
        Assert.Equal(1, snapshot.Revision);
        Assert.Equal(candidate.Exercises.Select(e => e.Id), snapshot.Exercises.Select(e => e.Id));
        Assert.Equal(new[] { row.ExerciseId, squat.ExerciseId }, snapshot.Exercises.Select(e => e.Metadata.ExerciseId));
        var loadedSquat = snapshot.Exercises[1];
        Assert.Equal(90, loadedSquat.RestSeconds);
        Assert.Equal(candidate.Exercises[1].Sets.Select(s => s.Id), loadedSquat.Sets.Select(s => s.Id));
        Assert.Equal(new[] { TemplateSetKind.Working, TemplateSetKind.Warmup }, loadedSquat.Sets.Select(s => s.Kind));
        Assert.Equal(new decimal?[] { 45.359m, 20.5m }, loadedSquat.Sets.Select(s => s.WeightKg));
        Assert.Equal(3, loadedSquat.Sets[0].RepMin);
        Assert.Equal(5, loadedSquat.Sets[0].RepMax);
        Assert.Equal(5, loadedSquat.Sets[1].RepMin);
        Assert.Equal(8, loadedSquat.Sets[1].RepMax);

        var stale = new TemplateEditorViewModel(store, scope, WeightUnit.Kg, culture);
        await stale.InitializeAsync(candidate.Id, ct);
        reopened.Name = "Updated elsewhere";
        Assert.True(await reopened.SaveAsync(ct));
        stale.Name = "Unsaved draft";
        Assert.False(await stale.SaveAsync(ct));
        Assert.Equal("Unsaved draft", stale.Name);
        Assert.True(stale.IsDirty);
        Assert.False(stale.SavedOnDevice);
        Assert.Contains("changed", stale.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(await stale.ReloadAsync(ct));
        Assert.Equal("Updated elsewhere", stale.Name);
        Assert.False(stale.IsDirty);
        Assert.True(await stale.ArchiveAsync(ct));

        var list = new TemplateListViewModel(reopenedStore, scope);
        await list.LoadAsync(ct);
        Assert.Empty(list.Items);
        list.IncludeArchived = true;
        await list.LoadAsync(ct);
        Assert.True(Assert.Single(list.Items).IsArchived);
        var otherAccount = TemplateScope.Create(scope.Server.ToString(), Guid.NewGuid());
        var otherServer = TemplateScope.Create("https://other.test/api/", scope.AccountId);
        foreach (var other in new[] { otherAccount, otherServer })
        {
            var isolated = new TemplateListViewModel(reopenedStore, other) { IncludeArchived = true };
            await isolated.LoadAsync(ct);
            Assert.Empty(isolated.Items);
            Assert.Null(await reopenedStore.GetAsync(other, candidate.Id, ct));
        }
    }

    private static LocalExerciseMetadata Metadata(TemplateScope scope, string name) => new(
        scope, Guid.NewGuid(), name, "Barbell", ExerciseTrackingType.ExternalWeightAndReps,
        ExerciseLoadConvention.TotalLoad, false);

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
