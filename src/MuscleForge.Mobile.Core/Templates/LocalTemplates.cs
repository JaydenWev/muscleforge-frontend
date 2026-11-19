using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Networking;

namespace MuscleForge.Mobile.Core.Templates;

/// <summary>A storage partition for a verified account or a local guest workspace. This is not an authorization credential.</summary>
public sealed record TemplateScope
{
    private TemplateScope(Uri server, Guid accountId)
    {
        Server = server;
        AccountId = accountId;
    }
    public Uri Server { get; }
    /// <summary>Account ID for a server partition; local workspace ID for a guest partition.</summary>
    public Guid AccountId { get; }
    /// <summary>True for the local-only storage namespace, which must never be used for HTTP.</summary>
    public bool IsGuest => Server.Scheme == "local";
    public static TemplateScope CreateGuest(Guid localWorkspaceId)
    {
        if (localWorkspaceId == Guid.Empty)
            throw new TemplateValidationException("A valid local workspace ID is required.");
        return new TemplateScope(new Uri("local://device/"), localWorkspaceId);
    }
    public static TemplateScope Create(string server, Guid accountId)
    {
        if (accountId == Guid.Empty || !ApiEndpoint.TryParse(server, out var uri))
        {
            throw new TemplateValidationException("A valid HTTPS server and account ID are required.");
        }
        return new TemplateScope(uri!, accountId);
    }
}

public enum ExerciseTrackingType { ExternalWeightAndReps }
public enum TemplateSetKind { Warmup, Working }

public sealed record LocalExerciseMetadata
{
    public LocalExerciseMetadata(TemplateScope scope, Guid exerciseId, string name, string equipment,
        ExerciseTrackingType tracking, ExerciseLoadConvention loadConvention, bool isArchived)
    {
        Scope = scope;
        ExerciseId = exerciseId;
        Name = name?.Trim()!;
        Equipment = equipment?.Trim()!;
        Tracking = tracking;
        LoadConvention = loadConvention;
        IsArchived = isArchived;
    }
    public TemplateScope Scope { get; }
    public Guid ExerciseId { get; }
    public string Name { get; }
    public string Equipment { get; }
    public ExerciseTrackingType Tracking { get; }
    public ExerciseLoadConvention LoadConvention { get; }
    public bool IsArchived { get; }
}
public sealed record LocalTemplateSet(Guid Id, int Position, TemplateSetKind Kind, int? RepMin, int? RepMax, decimal? WeightKg);

public sealed class LocalTemplateExercise
{
    public LocalTemplateExercise(Guid id, int position, LocalExerciseMetadata metadata, int? restSeconds, IEnumerable<LocalTemplateSet> sets)
    {
        Id = id;
        Position = position;
        Metadata = metadata;
        RestSeconds = restSeconds;
        Sets = new ReadOnlyCollection<LocalTemplateSet>((sets ?? throw new ArgumentNullException(nameof(sets))).ToArray());
    }
    public Guid Id { get; }
    public int Position { get; }
    public LocalExerciseMetadata Metadata { get; }
    public int? RestSeconds { get; }
    public IReadOnlyList<LocalTemplateSet> Sets { get; }
}

public sealed class LocalTemplate
{
    public LocalTemplate(TemplateScope scope, Guid id, string name, long revision, DateTimeOffset? archivedAt, IEnumerable<LocalTemplateExercise> exercises)
    {
        Scope = scope;
        Id = id;
        Name = name?.Trim()!;
        Revision = revision;
        ArchivedAt = archivedAt;
        Exercises = new ReadOnlyCollection<LocalTemplateExercise>((exercises ?? throw new ArgumentNullException(nameof(exercises))).ToArray());
    }
    public TemplateScope Scope { get; }
    public Guid Id { get; }
    public string Name { get; }
    public long Revision { get; }
    public DateTimeOffset? ArchivedAt { get; }
    public bool IsArchived => ArchivedAt.HasValue;
    public IReadOnlyList<LocalTemplateExercise> Exercises { get; }
}

public sealed record LocalTemplateSummary(Guid Id, string Name, long Revision, DateTimeOffset? ArchivedAt)
{
    public bool IsArchived => ArchivedAt.HasValue;
}

public interface ILocalTemplateStore
{
    Task<LocalTemplate?> GetAsync(TemplateScope scope, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalTemplateSummary>> ListAsync(TemplateScope scope, int offset, int limit, bool includeArchived, CancellationToken cancellationToken = default);
    Task<LocalTemplate> SaveAsync(TemplateScope scope, LocalTemplate template, long expectedRevision, CancellationToken cancellationToken = default);
    Task<LocalTemplate> ArchiveAsync(TemplateScope scope, Guid id, long expectedRevision, CancellationToken cancellationToken = default);
}

public sealed class TemplateValidationException(string message) : Exception(message);
public sealed class TemplateConflictException(string message) : Exception(message);
public sealed class TemplateNotFoundException(string message) : Exception(message);
public sealed class TemplateStorageException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class LocalTemplateValidation
{
    public const int MaximumPageSize = 100;
    public static void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > MaximumPageSize)
        {
            throw new TemplateValidationException("Invalid page bounds.");
        }
    }

    public static void ValidateForSave(TemplateScope scope, LocalTemplate template, LocalTemplate? existing)
    {
        if (scope is null || template is null || template.Scope != scope || template.Id == Guid.Empty || template.Revision < 0 ||
            !ValidName(template.Name) || template.Exercises.Count > 100 ||
            (template.ArchivedAt.HasValue && template.ArchivedAt.Value.Offset != TimeSpan.Zero))
        {
            Fail();
        }
        if (existing is null)
        {
            if (template.Revision != 0 || template.IsArchived)
            {
                Fail();
            }
        }
        else if (existing.Scope != scope || existing.Id != template.Id || template.ArchivedAt != existing.ArchivedAt || existing.IsArchived)
        {
            Fail();
        }
        var ids = new HashSet<Guid> { template.Id };
        var positions = new HashSet<int>();
        foreach (var exercise in template.Exercises)
        {
            if (exercise is null || exercise.Metadata is null || !ids.Add(exercise.Id) || exercise.Id == Guid.Empty ||
                !Position(exercise.Position, positions) || exercise.RestSeconds is < 0 or > 86400 || exercise.Sets.Count > 100)
            {
                Fail();
            }
            var metadata = exercise.Metadata;
            if (metadata.Scope != scope || metadata.ExerciseId == Guid.Empty || !ValidName(metadata.Name) || !ValidName(metadata.Equipment) ||
                !Enum.IsDefined(metadata.Tracking) || !Enum.IsDefined(metadata.LoadConvention))
            {
                Fail();
            }
            if (metadata.IsArchived && !(existing?.Exercises.Any(e => e.Id == exercise.Id && e.Metadata.ExerciseId == metadata.ExerciseId) ?? false))
            {
                Fail();
            }
            var setPositions = new HashSet<int>();
            foreach (var set in exercise.Sets)
            {
                if (set is null || set.Id == Guid.Empty || !ids.Add(set.Id) || !Position(set.Position, setPositions) ||
                    !Enum.IsDefined(set.Kind) || set.RepMin is <= 0 || set.RepMax is <= 0 ||
                    set.RepMin.HasValue != set.RepMax.HasValue ||
                    (set.RepMin.HasValue && set.RepMax.HasValue && set.RepMin > set.RepMax) ||
                    (set.WeightKg.HasValue && !Weight.TryToKilograms(set.WeightKg.Value, WeightUnit.Kg, out _)))
                {
                    Fail();
                }
            }
        }
    }
    public static bool TryCanonicalWeight(decimal value, WeightUnit unit, out decimal kilograms) => Weight.TryToKilograms(value, unit, out kilograms);
    private static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= 100;
    private static bool Position(int value, HashSet<int> positions) => value is >= 0 and <= 99 && positions.Add(value);
    [DoesNotReturn]
    private static void Fail() => throw new TemplateValidationException("Invalid local template.");
}
