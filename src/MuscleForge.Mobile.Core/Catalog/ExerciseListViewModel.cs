using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.Catalog;

public sealed class ExerciseListViewModel : ObservableObject, IDisposable
{
    private readonly IExerciseCatalogGateway gateway;
    private readonly IAuthSession session;
    private readonly Action<Action> dispatch;
    private CancellationTokenSource? operation;
    private long generation;
    private readonly Guid? accountId;
    private bool disposed, invalidated, isBusy, includeArchived, hasMore, loaded;
    private string searchText = "", errorMessage = "";
    private string? query;
    private bool queryArchived;
    private int nextOffset;
    public ExerciseListViewModel(IExerciseCatalogGateway gateway, IAuthSession session, Action<Action>? dispatch = null)
    {
        this.gateway = gateway; this.session = session; this.dispatch = dispatch ?? (a => a()); accountId = session.State.Account?.Id;
        SearchCommand = new AsyncRelayCommand(ct => LoadAsync(ct), AsyncRelayCommandOptions.AllowConcurrentExecutions);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        RetryCommand = new AsyncRelayCommand(ct => RetryAsync(ct));
        CancelCommand = new RelayCommand(Cancel);
        session.StateChanged += OnStateChanged;
    }
    public ObservableCollection<Exercise> Items { get; } = [];
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public bool IncludeArchived { get => includeArchived; set => SetProperty(ref includeArchived, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
    public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
    public bool IsEmpty => loaded && !IsBusy && Items.Count == 0 && ErrorMessage.Length == 0;
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand LoadMoreCommand { get; }
    public IAsyncRelayCommand RetryCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || invalidated) return Task.CompletedTask;
        query = SearchText?.Trim(); queryArchived = IncludeArchived; nextOffset = 0;
        Items.Clear(); loaded = false; HasMore = false; OnPropertyChanged(nameof(IsEmpty));
        if (query?.Length > 100) { Cancel(); ErrorMessage = "Search must be 100 characters or fewer."; return Task.CompletedTask; }
        return FetchAsync(false, cancellationToken);
    }
    public Task LoadMoreAsync(CancellationToken cancellationToken = default) => !disposed && !invalidated && HasMore && !IsBusy ? FetchAsync(true, cancellationToken) : Task.CompletedTask;
    private Task RetryAsync(CancellationToken ct) => disposed || invalidated ? Task.CompletedTask : query?.Length > 100 ? LoadAsync(ct) : FetchAsync(nextOffset > 0, ct);
    private async Task FetchAsync(bool append, CancellationToken ct)
    {
        Cancel(); var version = ++generation; var local = CancellationTokenSource.CreateLinkedTokenSource(ct); operation = local;
        var search = query; var archived = queryArchived; var offset = append ? nextOffset : 0;
        IsBusy = true; ErrorMessage = ""; OnPropertyChanged(nameof(IsEmpty));
        try
        {
            var page = await gateway.ListAsync(search, archived, offset, 50, local.Token);
            if (disposed || invalidated || version != generation || local.IsCancellationRequested) return;
            if (!append) Items.Clear();
            var existing = Items.Select(item => item.Id).ToHashSet();
            foreach (var item in page.Items) if (existing.Add(item.Id)) Items.Add(item);
            loaded = true;
            nextOffset = page.Offset + page.Items.Count; HasMore = page.Items.Count > 0 && nextOffset < page.TotalCount;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!disposed && !invalidated && version == generation && !local.IsCancellationRequested) ErrorMessage = "Could not load exercises. Please try again."; }
        finally { if (version == generation && !disposed) { IsBusy = false; operation = null; OnPropertyChanged(nameof(IsEmpty)); } local.Dispose(); }
    }
    public void Cancel() { var wasBusy = IsBusy; generation++; operation?.Cancel(); operation = null; IsBusy = false; if (wasBusy) ErrorMessage = "Loading cancelled. Try again when ready."; OnPropertyChanged(nameof(IsEmpty)); }
    private void OnStateChanged(object? sender, SessionState state)
    {
        if (state.Status == AuthStatus.SignedIn && state.Account?.Id == accountId) return;
        invalidated = true;
        dispatch(() => { if (!disposed) { Cancel(); ClearQuery(); Items.Clear(); ErrorMessage = ""; HasMore = false; OnPropertyChanged(nameof(IsEmpty)); } });
    }
    private void ClearQuery() { loaded = false; SearchText = ""; query = null; IncludeArchived = false; queryArchived = false; nextOffset = 0; }
    public void Dispose() { if (disposed) return; Cancel(); ClearQuery(); disposed = true; session.StateChanged -= OnStateChanged; Items.Clear(); ErrorMessage = ""; }
}
