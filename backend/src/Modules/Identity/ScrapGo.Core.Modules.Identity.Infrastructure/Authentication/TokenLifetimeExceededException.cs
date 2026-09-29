using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Thrown specifically when a token's <c>exp - iat</c> exceeds the platform's
/// 1-hour cap. It is a distinct type so the bearer scheme's <c>OnChallenge</c>
/// handler can surface the <c>token_lifetime_exceeded</c> reason for this case
/// only. Every other lifetime failure (ordinary expiry, not-yet-valid, nbf
/// after exp) keeps returning a bare 401.
/// </summary>
public sealed class TokenLifetimeExceededException(string message) : SecurityTokenInvalidLifetimeException(message)
{
    /// <summary>The 401 <c>reason</c> extension member value.</summary>
    public const string Reason = "token_lifetime_exceeded";
}
