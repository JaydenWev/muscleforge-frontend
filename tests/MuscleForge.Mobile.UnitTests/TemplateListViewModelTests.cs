using MuscleForge.Mobile.Core.Templates;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class TemplateListViewModelTests
{
    private static readonly TemplateScope Scope = TemplateScope.Create("https://example.com", Guid.NewGuid());

    [Fact]
    public async Task Empty_and_archived_filter_are_reflected_after_load()
    {
        var store = new Store();
        var vm = new TemplateListViewModel(store, Scope);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(vm.IsEmpty);
        vm.IncludeArchived = true;
        store.Page = [new(Guid.NewGuid(), "Archived", 1, DateTimeOffset.UtcNow)];
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(store.Archived);
        Assert.False(vm.IsEmpty);
        Assert.True(Assert.Single(vm.Items).IsArchived);
    }

    [Fact]
    public async Task Paging_uses_offsets_and_failures_preserve_loaded_items()
    {
        var store = new Store { Page = Enumerable.Range(0, 100).Select(i => new LocalTemplateSummary(Guid.NewGuid(), $"Plan {i}", 1, null)).ToArray() };
        var vm = new TemplateListViewModel(store, Scope);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(vm.HasMore);
        store.Failure = new TemplateStorageException("disk");
        await vm.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100, vm.Items.Count);
        Assert.NotEmpty(vm.ErrorMessage);
        store.Failure = new OperationCanceledException();
        await vm.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Contains("cancelled", vm.ErrorMessage);
        store.Failure = null;
        store.Page = [new(Guid.NewGuid(), "Next", 1, null)];
        await vm.LoadMoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100, store.Offset);
        Assert.Equal(101, vm.Items.Count);
        Assert.False(vm.HasMore);
    }

    private sealed class Store : ILocalTemplateStore
    {
        public IReadOnlyList<LocalTemplateSummary> Page = [];
        public Exception? Failure;
        public int Offset;
        public bool Archived;
        public Task<IReadOnlyList<LocalTemplateSummary>> ListAsync(TemplateScope scope, int offset, int limit, bool includeArchived, CancellationToken cancellationToken = default)
        {
            Assert.Equal(Scope, scope);
            Offset = offset;
            Archived = includeArchived;
            return Failure is null ? Task.FromResult(Page) : Task.FromException<IReadOnlyList<LocalTemplateSummary>>(Failure);
        }
        public Task<LocalTemplate?> GetAsync(TemplateScope scope, Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LocalTemplate> SaveAsync(TemplateScope scope, LocalTemplate template, long expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LocalTemplate> ArchiveAsync(TemplateScope scope, Guid id, long expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
