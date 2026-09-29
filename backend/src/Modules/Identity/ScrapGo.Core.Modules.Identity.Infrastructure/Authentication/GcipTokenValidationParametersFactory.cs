using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Single source of truth for the rules a GCIP ID token must satisfy: the
/// deployment's issuer and audience, a signature resolved via
/// <see cref="IGcipSigningKeyProvider"/>, RS256 only, and the 1-hour lifetime
/// cap. Shared by the bearer scheme and <see cref="GcipIdTokenValidator"/>, so
/// the re-auth token path can never drift from the header path.
/// </summary>
public static class GcipTokenValidationParametersFactory
{
    public static TokenValidationParameters Create(GcipOptions options, IGcipSigningKeyProvider signingKeyProvider) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,

            ValidateAudience = true,
            ValidAudience = options.ProjectId,

            ValidateLifetime = true,
            LifetimeValidator = GcipTokenLifetimeValidator.Validate,

            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) =>
                signingKeyProvider.GetSigningKeysAsync().GetAwaiter().GetResult(),

            // An explicit allowlist: never accept `alg: none` or an HMAC
            // algorithm (JWT algorithm confusion). GCIP signs RS256.
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };
}
