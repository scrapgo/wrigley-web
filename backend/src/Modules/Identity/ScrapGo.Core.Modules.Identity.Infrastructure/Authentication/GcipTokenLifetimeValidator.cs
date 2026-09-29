using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Custom <see cref="TokenValidationParameters.LifetimeValidator"/> enforcing
/// the platform's 1-hour cap: only GCIP tokens with <c>exp - iat ≤ 3600</c>
/// are accepted.
/// </summary>
/// <remarks>
/// <para>
/// Supplying a <c>LifetimeValidator</c> replaces the built-in lifetime check
/// entirely, so this method first reproduces the ordinary "within the
/// nbf/exp window" check itself (honoring <see cref="TokenValidationParameters.ClockSkew"/>,
/// same as the built-in validator), then applies the cap.
/// </para>
/// <para>
/// <c>securityToken</c> is a <see cref="JsonWebToken"/>: the JwtBearer
/// handler validates via <c>JsonWebTokenHandler</c>, not the legacy
/// <c>JwtSecurityTokenHandler</c>.
/// </para>
/// </remarks>
public static class GcipTokenLifetimeValidator
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(1);

    public static bool Validate(
        DateTime? notBefore,
        DateTime? expires,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        var clockSkew = validationParameters.ClockSkew;
        var utcNow = DateTime.UtcNow;

        // Ordinary lifetime window, mirroring the built-in validator this
        // delegate replaces.
        if (expires is null)
        {
            throw new SecurityTokenNoExpirationException("Token has no 'exp' claim.");
        }

        if (notBefore > expires)
        {
            throw new SecurityTokenInvalidLifetimeException("Token is invalid: 'nbf' is after 'exp'.")
            {
                NotBefore = notBefore.Value,
                Expires = expires.Value,
            };
        }

        if (notBefore.HasValue && utcNow < notBefore.Value - clockSkew)
        {
            throw new SecurityTokenNotYetValidException($"Token is not valid before {notBefore.Value}.")
            {
                NotBefore = notBefore.Value,
            };
        }

        if (utcNow > expires.Value + clockSkew)
        {
            throw new SecurityTokenExpiredException($"Token expired at {expires.Value}.")
            {
                Expires = expires.Value,
            };
        }

        // The 1-hour cap. iat has to be read off the token itself, and its
        // presence is checked explicitly: JsonWebToken.IssuedAt silently
        // returns DateTime.MinValue when the claim is absent, which would look
        // like a token genuinely issued at the Unix epoch.
        if (securityToken is not JsonWebToken jsonWebToken
            || !jsonWebToken.TryGetClaim(JwtRegisteredClaimNames.Iat, out _))
        {
            throw new SecurityTokenInvalidLifetimeException(
                "Token is missing a valid 'iat' claim; the 1-hour lifetime cap cannot be verified without it.");
        }

        var lifetime = expires.Value - jsonWebToken.IssuedAt;

        if (lifetime > MaxLifetime)
        {
            throw new TokenLifetimeExceededException(
                $"Token lifetime (exp - iat = {lifetime.TotalSeconds}s) exceeds the {MaxLifetime.TotalSeconds}s cap.");
        }

        return true;
    }
}
