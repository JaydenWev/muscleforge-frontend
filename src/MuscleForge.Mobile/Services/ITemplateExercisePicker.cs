using MuscleForge.Mobile.Core.Templates;

namespace MuscleForge.Mobile.Services;

public interface ITemplateExercisePicker
{
    Task<LocalExerciseMetadata?> PickAsync(TemplateScope scope, CancellationToken cancellationToken = default);
}
