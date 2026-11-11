namespace MuscleForge.Mobile.Core.Catalog;

public enum ExerciseLoadConvention { TotalLoad, PerDumbbell }
public sealed record Exercise(Guid Id, string Name, string Equipment, ExerciseLoadConvention LoadConvention, bool IsArchived, DateTimeOffset? ArchivedAt)
{
    public string LoadGuidance => LoadConvention == ExerciseLoadConvention.PerDumbbell
        ? "Record the weight of one dumbbell." : "Record the total external load, including the bar.";
}
public sealed record ExercisePage(IReadOnlyList<Exercise> Items, int Offset, int Limit, int TotalCount);
public sealed record ExerciseDraft(string Name, string Equipment, ExerciseLoadConvention LoadConvention, bool IsArchived = false);
public sealed record ExerciseSelection(Uri BaseUri, Guid AccountId, Guid ExerciseId);
public interface IExercisePicker { Task<ExerciseSelection?> PickAsync(CancellationToken cancellationToken = default); }
public interface IExerciseCatalogGateway
{
    Task<ExercisePage> ListAsync(string? search, bool includeArchived, int offset, int limit, CancellationToken cancellationToken = default);
    Task<Exercise> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Exercise> CreateAsync(ExerciseDraft draft, CancellationToken cancellationToken = default);
    Task<Exercise> UpdateAsync(Guid id, ExerciseDraft draft, CancellationToken cancellationToken = default);
    Task<ExerciseSelection?> SelectAsync(Guid id, CancellationToken cancellationToken = default);
}
