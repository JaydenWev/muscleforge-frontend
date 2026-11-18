using System.Globalization;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class TemplateEditorViewModelTests
{
    private static readonly TemplateScope Scope = TemplateScope.Create("https://example.com", Guid.NewGuid());
    private static LocalExerciseMetadata Metadata(bool archived = false) => new(Scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, archived);
    private static TemplateEditorViewModel Editor(Store store, WeightUnit unit = WeightUnit.Kg) => new(store, Scope, unit, CultureInfo.GetCultureInfo("en-US"));
    [Fact]
    public async Task Failed_and_conflicting_saves_retain_edits_and_revision()
    {
        var store = new Store(); var vm = Editor(store); await vm.InitializeAsync(ct: TestContext.Current.CancellationToken); vm.Name = "Plan"; Assert.True(vm.AddExercise(Metadata()));
        Assert.True(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.True(vm.SavedOnDevice); vm.Name = "Changed";
        store.Failure = new TemplateConflictException("stale"); Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.True(vm.IsDirty); Assert.False(vm.SavedOnDevice); Assert.Equal("Changed", vm.Name); Assert.Equal(1, vm.CreateSnapshot().Revision);
        store.Failure = new TemplateStorageException("disk"); Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.Equal("Changed", vm.Name); Assert.NotEmpty(vm.ErrorMessage);
    }
    [Fact]
    public async Task Snapshot_preserves_exact_kilograms_and_order_after_unit_switch()
    {
        var metadata = Metadata(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var store = new Store { Item = new(Scope, Guid.NewGuid(), "Plan", 3, null, [new(first, 0, metadata, 0, [new(Guid.NewGuid(), 0, TemplateSetKind.Warmup, 5, 8, 45.359m)]), new(second, 1, Metadata(), null, [])]) };
        var vm = Editor(store); await vm.InitializeAsync(store.Item.Id, TestContext.Current.CancellationToken); Assert.True(vm.SetWeightUnit(WeightUnit.Lb));
        vm.MoveExercise(vm.Exercises[1], -1); var snapshot = vm.CreateSnapshot();
        Assert.Equal(second, snapshot.Exercises[0].Id); Assert.Equal(1, snapshot.Exercises[1].Position); Assert.Equal(45.359m, snapshot.Exercises[1].Sets[0].WeightKg); Assert.Equal(0, snapshot.Exercises[1].RestSeconds);
    }
    [Fact]
    public async Task Strict_input_and_archived_selection_are_validated()
    {
        var vm = Editor(new Store()); await vm.InitializeAsync(ct: TestContext.Current.CancellationToken); vm.Name = "Plan"; Assert.False(vm.AddExercise(Metadata(true))); Assert.True(vm.AddExercise(Metadata()));
        var exercise = vm.Exercises[0]; Assert.True(exercise.AddSet()); var set = exercise.Sets[0]; set.WeightText = "1,000";
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.NotEmpty(vm.ValidationMessage); set.WeightText = "0"; set.RepMinText = "3";
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken)); set.RepMaxText = "5"; Assert.True(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.Equal(0m, vm.CreateSnapshot().Exercises[0].Sets[0].WeightKg);
    }
    [Fact]
    public async Task Edits_made_during_write_remain_dirty()
    {
        var pending = new TaskCompletionSource<LocalTemplate>(); var store = new Store { Pending = pending }; var vm = Editor(store); await vm.InitializeAsync(ct: TestContext.Current.CancellationToken); vm.Name = "Before";
        var saving = vm.SaveAsync(TestContext.Current.CancellationToken); vm.Name = "After"; var candidate = store.Last!; pending.SetResult(new(Scope, candidate.Id, candidate.Name, 1, null, candidate.Exercises));
        Assert.True(await saving); Assert.True(vm.IsDirty); Assert.False(vm.SavedOnDevice); Assert.Equal("After", vm.Name);
    }
    [Fact]
    public async Task Conflict_can_be_reloaded_and_saved_again()
    {
        var store = new Store();
        var vm = Editor(store);
        await vm.InitializeAsync(ct: TestContext.Current.CancellationToken);
        vm.Name = "Original";
        Assert.True(await vm.SaveAsync(TestContext.Current.CancellationToken));
        vm.Name = "Local edit";
        store.Failure = new TemplateConflictException("stale");
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        store.Failure = null;
        store.Item = new(Scope, store.Item!.Id, "Remote edit", 2, null, []);
        Assert.True(await vm.ReloadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Remote edit", vm.Name);
        Assert.False(vm.IsDirty);
        vm.Name = "Resolved";
        Assert.True(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, store.Item!.Revision);
    }

    [Fact]
    public async Task Failed_cancelled_and_late_reload_preserve_edits()
    {
        var store = new Store();
        var vm = Editor(store);
        await vm.InitializeAsync(ct: TestContext.Current.CancellationToken);
        vm.Name = "Saved";
        Assert.True(await vm.SaveAsync(TestContext.Current.CancellationToken));
        vm.Name = "Keep me";
        store.ReadFailure = new TemplateStorageException("disk");
        Assert.False(await vm.ReloadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Keep me", vm.Name);
        store.ReadFailure = new OperationCanceledException();
        Assert.False(await vm.ReloadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Keep me", vm.Name);
        store.ReadFailure = null;
        var pending = new TaskCompletionSource<LocalTemplate?>();
        store.PendingRead = pending;
        var reload = vm.ReloadAsync(TestContext.Current.CancellationToken);
        vm.Name = "Newer edit";
        pending.SetResult(store.Item);
        Assert.False(await reload);
        Assert.True(vm.IsDirty);
        Assert.Equal("Newer edit", vm.Name);
    }

    [Fact]
    public async Task Validation_identifies_fields_and_retains_draft()
    {
        var vm = Editor(new Store());
        await vm.InitializeAsync(ct: TestContext.Current.CancellationToken);
        vm.Name = " ";
        Assert.True(vm.AddExercise(Metadata()));
        var exercise = vm.Exercises[0];
        Assert.True(exercise.AddSet());
        var set = exercise.Sets[0];
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.Contains("name", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(" ", vm.Name);

        vm.Name = "Plan";
        exercise.RestSecondsText = "86401";
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.Contains("rest", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("86401", exercise.RestSecondsText);

        exercise.RestSecondsText = "0";
        set.RepMinText = "8";
        set.RepMaxText = "5";
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.Contains("repetition", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("8", set.RepMinText);
        Assert.Equal("5", set.RepMaxText);
        Assert.True(vm.IsDirty);
    }

    private sealed class Store : ILocalTemplateStore
    {
        public LocalTemplate? Item, Last; public Exception? Failure, ReadFailure; public TaskCompletionSource<LocalTemplate>? Pending; public TaskCompletionSource<LocalTemplate?>? PendingRead;
        public Task<LocalTemplate?> GetAsync(TemplateScope scope, Guid id, CancellationToken cancellationToken = default) => ReadFailure is not null ? Task.FromException<LocalTemplate?>(ReadFailure) : PendingRead?.Task ?? Task.FromResult(Item);
        public Task<IReadOnlyList<LocalTemplateSummary>> ListAsync(TemplateScope scope, int offset, int limit, bool includeArchived, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LocalTemplateSummary>>([]);
        public Task<LocalTemplate> SaveAsync(TemplateScope scope, LocalTemplate template, long expectedRevision, CancellationToken cancellationToken = default)
        {
            Last = template; if (Failure is not null) return Task.FromException<LocalTemplate>(Failure); if (Pending is not null) return Pending.Task;
            Item = new(scope, template.Id, template.Name, expectedRevision + 1, null, template.Exercises); return Task.FromResult(Item);
        }
        public Task<LocalTemplate> ArchiveAsync(TemplateScope scope, Guid id, long expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
