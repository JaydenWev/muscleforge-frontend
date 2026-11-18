using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MuscleForge.Mobile.Core.Templates;

public sealed class TemplateListViewModel : ObservableObject
{
    private readonly ILocalTemplateStore store;
    private readonly TemplateScope scope;
    private readonly Action<Action> dispatch;
    private bool busy, loaded, includeArchived, hasMore;
    private string error = "";
    private int offset;
    private bool queryArchived;
    public TemplateListViewModel(ILocalTemplateStore store, TemplateScope scope, Action<Action>? dispatch = null)
    {
        this.store = store;
        this.scope = scope;
        this.dispatch = dispatch ?? (a => a());
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
    }
    public ObservableCollection<LocalTemplateSummary> Items { get; } = [];
    public bool IncludeArchived { get => includeArchived; set => SetProperty(ref includeArchived, value); }
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(IsEmpty)); } }
    public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
    public bool IsEmpty => loaded && !IsBusy && Items.Count == 0 && ErrorMessage.Length == 0;
    public string ErrorMessage { get => error; private set { SetProperty(ref error, value); OnPropertyChanged(nameof(IsEmpty)); } }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand LoadMoreCommand { get; }
    public Task LoadAsync(CancellationToken ct = default)
    {
        if (IsBusy) return Task.CompletedTask;
        queryArchived = IncludeArchived;
        HasMore = false;
        return FetchAsync(false, ct);
    }
    public Task LoadMoreAsync(CancellationToken ct = default) => IsBusy || !HasMore ? Task.CompletedTask : FetchAsync(true, ct);
    private async Task FetchAsync(bool append, CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var page = await store.ListAsync(scope, append ? offset : 0, 100, queryArchived, ct);
            ct.ThrowIfCancellationRequested();
            await DispatchAsync(() =>
            {
                if (!append) Items.Clear();
                foreach (var item in page) Items.Add(item);
                offset = Items.Count;
                HasMore = page.Count == 100;
                loaded = true;
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
        catch (OperationCanceledException)
        {
            await DispatchAsync(() => ErrorMessage = "Loading cancelled. Try again when ready.");
        }
        catch (Exception)
        {
            await DispatchAsync(() => ErrorMessage = "Could not load templates. Please try again.");
        }
        finally
        {
            await DispatchAsync(() => IsBusy = false);
        }
    }
    private Task DispatchAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatch(() => { try { action(); completion.SetResult(); } catch (Exception error) { completion.SetException(error); } });
        return completion.Task;
    }

}
