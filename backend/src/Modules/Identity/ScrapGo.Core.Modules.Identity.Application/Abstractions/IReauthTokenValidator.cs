namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// Validates a raw GCIP ID token that arrived somewhere other than the
/// <c>Authorization</c> header (e.g. the account-linking request body) against
/// the same rules the bearer scheme enforces.
/// </summary>
public interface IReauthTokenValidator
{
    Task<ReauthTokenValidationResult> ValidateAsync(string rawToken, CancellationToken cancellationToken);
}

/// <param name="Uid">The token's <c>sub</c> claim. Always non-null when <see cref="IsValid"/>.</param>
/// <param name="AuthTime">
/// When the subject last actively authenticated (<c>auth_time</c>), as distinct
/// from <c>iat</c>: a silently refreshed token from an old session still has
/// a fresh <c>iat</c>.
/// </param>
/// <param name="SignInProvider">The <c>firebase.sign_in_provider</c> claim. Informational only, never part of a security decision.</param>
public sealed record ReauthTokenValidationResult(
    bool IsValid,
    string? Uid = null,
    DateTimeOffset? AuthTime = null,
    string? SignInProvider = null,
    string? Email = null,
    string? HostedDomain = null)
{
    public static readonly ReauthTokenValidationResult Invalid = new(IsValid: false);
}
