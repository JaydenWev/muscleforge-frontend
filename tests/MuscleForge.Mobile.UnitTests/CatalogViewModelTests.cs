using System.Net;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Catalog;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class CatalogViewModelTests
{
    private static readonly Exercise Item = new(Guid.NewGuid(), "Squat", "Barbell", ExerciseLoadConvention.TotalLoad, false, null);
    [Fact]
    public async Task Late_search_response_cannot_replace_new_query()
    {
        var gateway = new FakeGateway(); var pending = new TaskCompletionSource<ExercisePage>();
        gateway.List = (query, _, _, _) => query == "old" ? pending.Task : Task.FromResult(new ExercisePage([Item], 0, 50, 1));
        using var vm = new ExerciseListViewModel(gateway, new FakeSession()) { SearchText = "old" };
        var old = vm.LoadAsync(TestContext.Current.CancellationToken); vm.SearchText = "new"; await vm.LoadAsync(TestContext.Current.CancellationToken);
        pending.SetResult(new ExercisePage([Item with { Name = "Old" }], 0, 50, 1)); await old;
        Assert.Equal("Squat", Assert.Single(vm.Items).Name);
    }
    [Fact]
    public async Task Failed_save_preserves_form_and_never_retries()
    {
        var gateway = new FakeGateway { FailWrite = true }; using var vm = new ExerciseEditorViewModel(gateway, new FakeSession()) { Name = "  Press  ", Equipment = " Dumbbell ", LoadConvention = ExerciseLoadConvention.PerDumbbell };
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.Equal(1, gateway.Writes); Assert.Equal("  Press  ", vm.Name); Assert.NotEmpty(vm.ErrorMessage); Assert.Null(vm.SavedExercise);
        vm.Name = " "; Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken)); Assert.Equal(1, gateway.Writes); Assert.NotEmpty(vm.ValidationMessage);
    }
    [Fact]
    public async Task Archived_edit_remains_archived_and_selection_is_rechecked()
    {
        var gateway = new FakeGateway(); var archived = Item with { IsArchived = true, ArchivedAt = DateTimeOffset.UtcNow };
        using var editor = new ExerciseEditorViewModel(gateway, new FakeSession(), archived);
        gateway.Loaded = archived; await editor.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(await editor.SaveAsync(TestContext.Current.CancellationToken)); Assert.True(gateway.Draft!.IsArchived);
        using var selector = new ExerciseSelectionViewModel(gateway, new FakeSession());
        gateway.Selection = null; Assert.Null(await selector.SelectAsync(Item, TestContext.Current.CancellationToken)); Assert.Equal(1, gateway.Selections); Assert.NotEmpty(selector.ErrorMessage);
    }
    [Fact]
    public async Task Logout_clears_and_ignores_late_uncooperative_result()
    {
        var gateway = new FakeGateway(); var pending = new TaskCompletionSource<ExercisePage>(); gateway.List = (_, _, _, _) => pending.Task;
        var session = new FakeSession(); using var vm = new ExerciseListViewModel(gateway, session);
        var loading = vm.LoadAsync(TestContext.Current.CancellationToken); Assert.True(vm.IsBusy); session.Logout();
        pending.SetResult(new ExercisePage([Item], 0, 50, 1)); await loading;
        Assert.Empty(vm.Items); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task Paging_deduplicates_ids_and_advances_by_raw_count()
    {
        var gateway = new FakeGateway(); var offsets = new List<int>();
        gateway.List = (_, _, offset, limit) =>
        {
            offsets.Add(offset);
            return Task.FromResult(offset == 0
                ? new ExercisePage([Item, Item], offset, limit, 3)
                : new ExercisePage([Item with { Id = Guid.NewGuid() }], offset, limit, 3));
        };
        using var vm = new ExerciseListViewModel(gateway, new FakeSession());
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        await vm.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 0, 2 }, offsets);
        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.HasMore);
    }
    [Fact]
    public async Task Cancel_and_dispose_reject_late_save_and_clear_form()
    {
        var gateway = new FakeGateway(); var pending = new TaskCompletionSource<Exercise>(); gateway.PendingWrite = pending;
        var vm = new ExerciseEditorViewModel(gateway, new FakeSession()) { Name = "Press", Equipment = "Barbell" };
        var save = vm.SaveAsync(TestContext.Current.CancellationToken);
        vm.Cancel(); vm.Dispose(); pending.SetResult(Item);
        Assert.False(await save);
        Assert.Null(vm.SavedExercise);
        Assert.Empty(vm.Name);
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Validation_response_preserves_editor_fields_without_retry()
    {
        var gateway = new FakeGateway { WriteError = new ApiException(HttpStatusCode.BadRequest,
            new ApiError { Errors = new Dictionary<string, string[]> { ["name"] = ["untrusted-private-data"] } }, null) };
        using var vm = new ExerciseEditorViewModel(gateway, new FakeSession())
        { Name = " Press ", Equipment = " Dumbbells ", LoadConvention = ExerciseLoadConvention.PerDumbbell };
        Assert.False(await vm.SaveAsync(TestContext.Current.CancellationToken));
        Assert.Equal(" Press ", vm.Name);
        Assert.Equal(" Dumbbells ", vm.Equipment);
        Assert.Equal(ExerciseLoadConvention.PerDumbbell, vm.LoadConvention);
        Assert.Null(vm.SavedExercise);
        Assert.Contains("fields", vm.ErrorMessage);
        Assert.Contains("Name", vm.ValidationMessage);
        Assert.DoesNotContain("untrusted-private-data", vm.ValidationMessage);
        Assert.Equal(1, gateway.Writes);
    }
    [Fact]
    public async Task Cancelled_picker_ignores_late_selection()
    {
        var gateway = new FakeGateway();
        var pending = new TaskCompletionSource<ExerciseSelection?>();
        gateway.PendingSelection = pending;
        using var vm = new ExerciseSelectionViewModel(gateway, new FakeSession());
        var selection = vm.SelectAsync(Item, TestContext.Current.CancellationToken);
        vm.Cancel();
        pending.SetResult(new ExerciseSelection(new Uri("https://example.test/"), Guid.NewGuid(), Item.Id));
        Assert.Null(await selection);
        Assert.False(vm.IsBusy);
    }
    private sealed class FakeGateway : IExerciseCatalogGateway
    {
        public Func<string?, bool, int, int, Task<ExercisePage>> List = (_, _, offset, limit) => Task.FromResult(new ExercisePage([], offset, limit, 0));
        public Exception? WriteError; public TaskCompletionSource<ExerciseSelection?>? PendingSelection; public TaskCompletionSource<Exercise>? PendingWrite; public Exercise Loaded = Item; public bool FailWrite; public int Writes, Selections; public ExerciseDraft? Draft; public ExerciseSelection? Selection;
        public Task<ExercisePage> ListAsync(string? search, bool includeArchived, int offset, int limit, CancellationToken cancellationToken = default) => List(search, includeArchived, offset, limit);
        public Task<Exercise> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Loaded);
        public Task<Exercise> CreateAsync(ExerciseDraft draft, CancellationToken cancellationToken = default) => UpdateAsync(Item.Id, draft, cancellationToken);
        public Task<Exercise> UpdateAsync(Guid id, ExerciseDraft draft, CancellationToken cancellationToken = default) { Writes++; Draft = draft; if (WriteError is not null) throw WriteError; if (FailWrite) throw new HttpRequestException(); return PendingWrite?.Task ?? Task.FromResult(Item with { IsArchived = draft.IsArchived }); }
        public Task<ExerciseSelection?> SelectAsync(Guid id, CancellationToken cancellationToken = default) { Selections++; return PendingSelection?.Task ?? Task.FromResult(Selection); }
    }
    private sealed class FakeSession : IAuthSession
    {
        public SessionState State { get; private set; } = new(AuthStatus.SignedIn, new AuthAccount(Guid.NewGuid(), "user@example.com"));
        public event EventHandler<SessionState>? StateChanged;
        public void Logout() { State = new(AuthStatus.SignedOut); StateChanged?.Invoke(this, State); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string email, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(CancellationToken cancellationToken = default) { Logout(); return Task.CompletedTask; }
        public Task<AuthContext?> GetContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(null);
        public Task<AuthContext?> RefreshAsync(AuthContext rejected, CancellationToken cancellationToken = default) => Task.FromResult<AuthContext?>(null);
    }
}
