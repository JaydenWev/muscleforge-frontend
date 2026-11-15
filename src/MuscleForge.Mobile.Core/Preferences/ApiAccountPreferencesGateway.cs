using System.Net;
using MuscleForge.Client;
using MuscleForge.Mobile.Core.Authentication;
using MuscleForge.Mobile.Core.Measurements;

namespace MuscleForge.Mobile.Core.Preferences;

public sealed class ApiAccountPreferencesGateway(MuscleForgeApiClient client, IAuthSession session) : IAccountPreferencesGateway
{
    private static bool Same(AuthContext? context, PreferencesScope scope) =>
        context is not null && context.AccountId == scope.AccountId && context.BaseUri == scope.BaseUri;

    private async Task<T> ExecuteAsync<T>(PreferencesScope scope, Func<AuthContext, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var context = await session.GetContextAsync(ct);
        if (!Same(context, scope)) throw new OperationCanceledException("Session changed.", ct);
        ct.ThrowIfCancellationRequested();
        T result;
        try { result = await action(context!, ct); }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await session.RefreshAsync(context!, ct);
            if (!Same(refreshed, scope)) throw new OperationCanceledException("Session changed.", ct);
            ct.ThrowIfCancellationRequested();
            result = await action(refreshed!, ct);
        }
        ct.ThrowIfCancellationRequested();
        if (!Same(await session.GetContextAsync(ct), scope)) throw new OperationCanceledException("Session changed.", ct);
        return result;
    }

    private static bool ValidTimeZoneId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 100 && id == id.Trim() && !id.Any(char.IsControl);

    private static AccountPreferences Map(AccountPreferencesResponse response)
    {
        if (response is null || !Enum.IsDefined(response.WeightUnit) || !ValidTimeZoneId(response.TimeZoneId))
            throw new InvalidOperationException("Invalid account preferences response.");
        return new(response.WeightUnit == AccountPreferencesResponseWeightUnit.Kg ? WeightUnit.Kg : WeightUnit.Lb, response.TimeZoneId);
    }

    public async Task<AccountPreferences> GetAsync(PreferencesScope scope, CancellationToken cancellationToken = default) =>
        Map(await ExecuteAsync(scope, (c, ct) => client.GetAccountPreferencesAsync(c.BaseUri, c.AccessToken, ct), cancellationToken));

    public async Task<AccountPreferences> SaveAsync(PreferencesScope scope, AccountPreferences preferences, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(preferences.WeightUnit) || !ValidTimeZoneId(preferences.TimeZoneId)) throw new ArgumentException("Invalid preferences.", nameof(preferences));
        var request = new UpdateAccountPreferencesRequest
        {
            WeightUnit = preferences.WeightUnit == WeightUnit.Kg ? UpdateAccountPreferencesRequestWeightUnit.Kg : UpdateAccountPreferencesRequestWeightUnit.Lb,
            TimeZoneId = preferences.TimeZoneId
        };
        return Map(await ExecuteAsync(scope, (c, ct) => client.UpdateAccountPreferencesAsync(c.BaseUri, c.AccessToken, request, ct), cancellationToken));
    }
}
