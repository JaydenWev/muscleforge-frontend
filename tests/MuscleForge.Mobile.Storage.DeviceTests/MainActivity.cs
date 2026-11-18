using Android.App;
using Android.OS;
using Android.Widget;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Storage;

namespace MuscleForge.Mobile.Storage.DeviceTests;

[Activity(Name = "local.muscleforge.storagechecks.MainActivity", Exported = true)]
public sealed class MainActivity : Activity
{
    private static readonly Guid Account = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TemplateId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ExerciseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CatalogId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Set1 = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Set2 = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Set3 = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static TemplateScope Scope => TemplateScope.Create("https://storage-smoke.invalid/", Account);

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var output = new TextView(this) { Text = "MF20 running" };
        SetContentView(output);
        var phase = Intent?.GetStringExtra("phase");
        try
        {
            var path = Path.Combine(FilesDir!.AbsolutePath, "mf20-smoke.db3");
            if (phase == "seed") await Seed(path);
            else if (phase == "verify") await Verify(path);
            else throw new ArgumentException("Unknown phase");
            output.Text = $"MF20 {phase} PASS";
            Android.Util.Log.Info("MF20StorageChecks", output.Text);
        }
        catch (Exception ex)
        {
            output.Text = $"MF20 {phase} FAIL {ex.GetType().Name}";
            Android.Util.Log.Error("MF20StorageChecks", output.Text);
        }
    }

    private static async Task Seed(string path)
    {
        if (File.Exists(path)) throw new InvalidOperationException("Probe database already exists");
        var metadata = new LocalExerciseMetadata(Scope, CatalogId, "Synthetic squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, false);
        var exercise = new LocalTemplateExercise(ExerciseId, 0, metadata, 90,
        [
            new LocalTemplateSet(Set1, 0, TemplateSetKind.Warmup, 5, 8, 45.359m),
            new LocalTemplateSet(Set2, 1, TemplateSetKind.Working, 1, 1, 9999999.999m),
            new LocalTemplateSet(Set3, 2, TemplateSetKind.Working, null, null, null)
        ]);
        var saved = await new SqliteLocalTemplateStore(path).SaveAsync(Scope, new LocalTemplate(Scope, TemplateId, "Synthetic", 0, null, [exercise]), 0);
        CheckGraph(saved, 1);
    }

    private static async Task Verify(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Probe database missing");
        var store = new SqliteLocalTemplateStore(path);
        var saved = await store.GetAsync(Scope, TemplateId) ?? throw new InvalidOperationException("Missing template");
        CheckGraph(saved, 1);
        var wrongScope = TemplateScope.Create("https://storage-smoke.invalid/", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Require(await store.GetAsync(wrongScope, TemplateId) is null);
        var update = new LocalTemplate(Scope, TemplateId, "Updated", saved.Revision, null, saved.Exercises);
        var updated = await store.SaveAsync(Scope, update, 1);
        CheckGraph(updated, 2, "Updated");
        try
        {
            await store.SaveAsync(Scope, saved, 1);
            throw new InvalidOperationException("Stale write accepted");
        }
        catch (TemplateConflictException) { }
        CheckGraph(await store.GetAsync(Scope, TemplateId) ?? throw new InvalidOperationException(), 2, "Updated");
        var archived = await store.ArchiveAsync(Scope, TemplateId, 2);
        Require(archived.ArchivedAt is not null);
        CheckGraph(archived, 3, "Updated");
        var reopened = await new SqliteLocalTemplateStore(path).GetAsync(Scope, TemplateId) ?? throw new InvalidOperationException();
        Require(reopened.ArchivedAt is not null);
        CheckGraph(reopened, 3, "Updated");
    }

    private static void CheckGraph(LocalTemplate template, long revision, string name = "Synthetic")
    {
        Require(template.Scope == Scope && template.Id == TemplateId && template.Revision == revision && template.Name == name);
        Require(template.Exercises.Count == 1);
        var exercise = template.Exercises[0];
        Require(exercise.Id == ExerciseId && exercise.Position == 0 && exercise.RestSeconds == 90);
        Require(exercise.Metadata.Scope == Scope && exercise.Metadata.ExerciseId == CatalogId && exercise.Metadata.Name == "Synthetic squat" && exercise.Metadata.Equipment == "Barbell" && exercise.Metadata.Tracking == ExerciseTrackingType.ExternalWeightAndReps && exercise.Metadata.LoadConvention == ExerciseLoadConvention.TotalLoad && !exercise.Metadata.IsArchived);
        Require(exercise.Sets.Count == 3);
        Require(exercise.Sets[0] == new LocalTemplateSet(Set1, 0, TemplateSetKind.Warmup, 5, 8, 45.359m));
        Require(exercise.Sets[1] == new LocalTemplateSet(Set2, 1, TemplateSetKind.Working, 1, 1, 9999999.999m));
        Require(exercise.Sets[2] == new LocalTemplateSet(Set3, 2, TemplateSetKind.Working, null, null, null));
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Probe assertion failed");
    }
}
