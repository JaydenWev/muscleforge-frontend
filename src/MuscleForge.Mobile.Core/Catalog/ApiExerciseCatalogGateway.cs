using System.Net;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;

namespace MuscleForge.Mobile.Core.Catalog;

public sealed class ApiExerciseCatalogGateway(MuscleForgeApiClient client, IAuthSession session) : IExerciseCatalogGateway
{
    private static bool Same(AuthContext? a, AuthContext b) => a is not null && a.AccountId == b.AccountId && a.BaseUri == b.BaseUri;
    private async Task<(T Result, AuthContext Context)> ExecuteAsync<T>(Func<AuthContext, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var context = await session.GetContextAsync(ct) ?? throw new InvalidOperationException("Sign in required.");
        T result;
        try { result = await action(context, ct); }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await session.RefreshAsync(context, ct);
            if (!Same(refreshed, context)) throw new OperationCanceledException("Session changed.", ct);
            context = refreshed!;
            result = await action(context, ct);
        }
        ct.ThrowIfCancellationRequested();
        if (!Same(await session.GetContextAsync(ct), context)) throw new OperationCanceledException("Session changed.", ct);
        return (result, context);
    }
    private static Exercise Map(ExerciseResponse e)
    {
        if (e is null || e.Id == Guid.Empty || string.IsNullOrWhiteSpace(e.Name) || e.Name.Trim().Length > 100 || string.IsNullOrWhiteSpace(e.Equipment) || e.Equipment.Trim().Length > 100 || e.TrackingType != ExerciseResponseTrackingType.ExternalWeightAndReps || e.IsArchived != e.ArchivedAt.HasValue)
            throw new InvalidOperationException("Invalid exercise response.");
        return new(e.Id, e.Name, e.Equipment, e.LoadConvention switch { ExerciseResponseLoadConvention.TotalLoad => ExerciseLoadConvention.TotalLoad, ExerciseResponseLoadConvention.PerDumbbell => ExerciseLoadConvention.PerDumbbell, _ => throw new InvalidOperationException("Unknown load convention.") }, e.IsArchived, e.ArchivedAt);
    }
    public async Task<ExercisePage> ListAsync(string? search, bool includeArchived, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var (page, _) = await ExecuteAsync((c, ct) => client.ListExercisesAsync(c.BaseUri, c.AccessToken, search, includeArchived, offset, limit, ct), cancellationToken);
        if (page is null || page.Items is null || page.Offset != offset || page.Limit != limit || page.TotalCount < 0 || page.Items.Count > limit) throw new InvalidOperationException("Invalid exercise page.");
        var items = page.Items.Select(Map).ToArray();
        if (!includeArchived && items.Any(item => item.IsArchived)) throw new InvalidOperationException("Invalid active exercise page.");
        return new(items, page.Offset, page.Limit, page.TotalCount);
    }
    public async Task<Exercise> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = Map((await ExecuteAsync((c, ct) => client.GetExerciseAsync(c.BaseUri, c.AccessToken, id, ct), cancellationToken)).Result);
        return item.Id == id ? item : throw new InvalidOperationException("Invalid exercise identity.");
    }
    public async Task<Exercise> CreateAsync(ExerciseDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft.IsArchived || !Enum.IsDefined(draft.LoadConvention)) throw new ArgumentOutOfRangeException(nameof(draft));
        var body = new CreateExerciseRequest { Name = draft.Name, Equipment = draft.Equipment, TrackingType = CreateExerciseRequestTrackingType.ExternalWeightAndReps, LoadConvention = draft.LoadConvention == ExerciseLoadConvention.PerDumbbell ? CreateExerciseRequestLoadConvention.PerDumbbell : CreateExerciseRequestLoadConvention.TotalLoad };
        return Map((await ExecuteAsync((c, ct) => client.CreateExerciseAsync(c.BaseUri, c.AccessToken, body, ct), cancellationToken)).Result);
    }
    public async Task<Exercise> UpdateAsync(Guid id, ExerciseDraft draft, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(draft.LoadConvention)) throw new ArgumentOutOfRangeException(nameof(draft));
        var body = new UpdateExerciseRequest { Name = draft.Name, Equipment = draft.Equipment, IsArchived = draft.IsArchived, TrackingType = UpdateExerciseRequestTrackingType.ExternalWeightAndReps, LoadConvention = draft.LoadConvention == ExerciseLoadConvention.PerDumbbell ? UpdateExerciseRequestLoadConvention.PerDumbbell : UpdateExerciseRequestLoadConvention.TotalLoad };
        var item = Map((await ExecuteAsync((c, ct) => client.UpdateExerciseAsync(c.BaseUri, c.AccessToken, id, body, ct), cancellationToken)).Result);
        return item.Id == id ? item : throw new InvalidOperationException("Invalid exercise identity.");
    }
    public async Task<ExerciseSelection?> SelectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (exercise, context) = await ExecuteAsync((c, ct) => client.GetExerciseAsync(c.BaseUri, c.AccessToken, id, ct), cancellationToken);
        var item = Map(exercise);
        if (item.Id != id) throw new InvalidOperationException("Invalid exercise identity.");
        return item.IsArchived ? null : new(context.BaseUri, context.AccountId, item.Id);
    }
}
