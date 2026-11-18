using System.Text.Json;
using MuscleForge.Mobile.Core.Catalog;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using Xunit;

namespace MuscleForge.Mobile.UnitTests;

public class LocalTemplateModelTests
{
    private static readonly TemplateScope Scope = TemplateScope.Create("https://EXAMPLE.test:443/api", Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static LocalExerciseMetadata Metadata(bool archived = false) => new(Scope, Guid.NewGuid(), "Squat", "Barbell", ExerciseTrackingType.ExternalWeightAndReps, ExerciseLoadConvention.TotalLoad, archived);
    private static LocalTemplateSet Set(Guid? id = null, decimal? kg = 45.359m) => new(id ?? Guid.NewGuid(), 0, TemplateSetKind.Working, 5, 8, kg);
    private static LocalTemplate Template(IEnumerable<LocalTemplateExercise> exercises, long revision = 0) => new(Scope, Guid.NewGuid(), " Strength ", revision, null, exercises);
    private static LocalTemplateExercise Exercise(LocalExerciseMetadata? metadata = null, IEnumerable<LocalTemplateSet>? sets = null) => new(Guid.NewGuid(), 2, metadata ?? Metadata(), 90, sets ?? [Set()]);

    [Fact]
    public void ScopeNormalizesAndRejectsInvalidIdentityShape()
    {
        Assert.Equal("https://example.test/api/", Scope.Server.AbsoluteUri);
        Assert.Equal(Scope, TemplateScope.Create("https://example.test/api/", Scope.AccountId));
        Assert.Throws<TemplateValidationException>(() => TemplateScope.Create("http://example.test", Scope.AccountId));
        Assert.Throws<TemplateValidationException>(() => TemplateScope.Create("https://user@example.test", Scope.AccountId));
        Assert.Throws<TemplateValidationException>(() => TemplateScope.Create("https://example.test", Guid.Empty));
    }

    [Fact]
    public void SnapshotCopiesListsAndTrimsName()
    {
        var sets = new List<LocalTemplateSet> { Set() };
        var exercises = new List<LocalTemplateExercise> { Exercise(sets: sets) };
        var template = Template(exercises);
        sets.Clear(); exercises.Clear();
        Assert.Equal("Strength", template.Name);
        Assert.Single(template.Exercises);
        Assert.Single(template.Exercises[0].Sets);
        LocalTemplateValidation.ValidateForSave(Scope, template, null);
    }

    [Fact]
    public void ArchivedExerciseCanOnlyBeRetainedUnderSameChildIdentity()
    {
        var exercise = Exercise(Metadata(true));
        var candidate = Template([exercise]);
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, candidate, null));
        var existing = new LocalTemplate(Scope, candidate.Id, candidate.Name, 1, null, [exercise]);
        var update = new LocalTemplate(Scope, candidate.Id, candidate.Name, 1, null, [exercise]);
        LocalTemplateValidation.ValidateForSave(Scope, update, existing);
        var replacement = new LocalTemplate(Scope, candidate.Id, candidate.Name, 1, null, [Exercise(exercise.Metadata)]);
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, replacement, existing));
    }

    [Fact]
    public void RejectsInvalidScopeIdsPositionsAndValues()
    {
        var exercise = Exercise();
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(TemplateScope.Create("https://other.test", Scope.AccountId), Template([exercise]), null));
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, Template([new LocalTemplateExercise(exercise.Id, 0, exercise.Metadata, 0, [Set(exercise.Id)])]), null));
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, Template([new LocalTemplateExercise(Guid.NewGuid(), 100, Metadata(), 0, [])]), null));
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, Template([Exercise(sets: [Set(kg: 1.0000m)])]), null));
        Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidatePage(0, 101));
    }

    [Fact]
    public void ValidatesBoundsRepetitionsAndDistinctPositions()
    {
        var metadata = Metadata();
        void Invalid(LocalTemplateExercise exercise) => Assert.Throws<TemplateValidationException>(() => LocalTemplateValidation.ValidateForSave(Scope, Template([exercise]), null));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, 86401, []));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, [new(Guid.NewGuid(), 0, TemplateSetKind.Working, 9, 8, null)]));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, [new(Guid.NewGuid(), 0, TemplateSetKind.Working, 0, null, null)]));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, [new(Guid.NewGuid(), 0, TemplateSetKind.Working, null, 8, null)]));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, [new(Guid.NewGuid(), 0, TemplateSetKind.Working, 5, null, null)]));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, [Set(), Set()]));
        Invalid(new LocalTemplateExercise(Guid.NewGuid(), 0, metadata, null, Enumerable.Range(0, 101).Select(i => new LocalTemplateSet(Guid.NewGuid(), i, TemplateSetKind.Working, null, null, null))));
        LocalTemplateValidation.ValidateForSave(Scope, Template([new LocalTemplateExercise(Guid.NewGuid(), 99, metadata, 86400, [new(Guid.NewGuid(), 99, TemplateSetKind.Warmup, null, null, 9_999_999.999m)])]), null);
    }

    [Fact]
    public void PinnedNumericFixtureUsesSharedConversion()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "units-timezones.json")));
        foreach (var item in document.RootElement.GetProperty("weightConversions").EnumerateArray())
        {
            var unit = item.GetProperty("unit").GetString() == "lb" ? WeightUnit.Lb : WeightUnit.Kg;
            Assert.True(LocalTemplateValidation.TryCanonicalWeight(item.GetProperty("value").GetDecimal(), unit, out var kg));
            Assert.Equal(item.GetProperty("kilograms").GetDecimal(), kg);
        }
    }

    [Fact]
    public void LocalTemplateFixtureRecordsCanonicalKilograms()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "local-template.json")));
        var root = document.RootElement;
        var example = root.GetProperty("weightExample");
        Assert.True(LocalTemplateValidation.TryCanonicalWeight(example.GetProperty("input").GetDecimal(), WeightUnit.Lb, out var kg));
        Assert.Equal(example.GetProperty("kilograms").GetDecimal(), kg);
        Assert.Equal(kg, root.GetProperty("exercises")[0].GetProperty("sets")[0].GetProperty("weightKg").GetDecimal());
    }
}
