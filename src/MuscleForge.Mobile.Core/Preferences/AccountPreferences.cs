using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Core.Preferences;

public sealed record AccountPreferences(WeightUnit WeightUnit, string TimeZoneId);
public sealed record PreferencesScope(Uri BaseUri, Guid AccountId);

public interface IAccountPreferencesGateway
{
    Task<AccountPreferences> GetAsync(PreferencesScope scope, CancellationToken cancellationToken = default);
    Task<AccountPreferences> SaveAsync(PreferencesScope scope, AccountPreferences preferences, CancellationToken cancellationToken = default);
}
