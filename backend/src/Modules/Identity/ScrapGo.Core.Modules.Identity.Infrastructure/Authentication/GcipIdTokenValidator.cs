using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// <see cref="IReauthTokenValidator"/> backed by the same
/// <see cref="JsonWebTokenHandler"/> and <see cref="GcipTokenValidationParametersFactory"/>
/// the bearer scheme uses. There is no hand-rolled validation for this second token.
/// </summary>
public sealed class GcipIdTokenValidator(IGcipSigningKeyProvider signingKeyProvider, IOptions<GcipOptions> options)
    : IReauthTokenValidator
{
    private static readonly JsonWebTokenHandler Handler = new();

    public async Task<ReauthTokenValidationResult> ValidateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var validationParameters = GcipTokenValidationParametersFactory.Create(options.Value, signingKeyProvider);

        TokenValidationResult result;
        try
        {
            // rawToken is an attacker-reachable request-body value with no
            // shape validation yet. ValidateTokenAsync reports ordinary
            // failures via IsValid, but it isn't documented never to throw on
            // input that isn't even JWT-shaped, so fail closed on any
            // exception rather than surfacing a 500.
            result = await Handler.ValidateTokenAsync(rawToken, validationParameters);
        }
        catch (Exception)
        {
            return ReauthTokenValidationResult.Invalid;
        }

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jsonWebToken || string.IsNullOrEmpty(jsonWebToken.Subject))
        {
            return ReauthTokenValidationResult.Invalid;
        }

        DateTimeOffset? authTime =
            jsonWebToken.TryGetClaim("auth_time", out var authTimeClaim) && long.TryParse(authTimeClaim.Value, out var epochSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds)
                : null;

        string? signInProvider =
            jsonWebToken.TryGetPayloadValue<JsonElement>("firebase", out var firebase)
            && firebase.ValueKind == JsonValueKind.Object
            && firebase.TryGetProperty("sign_in_provider", out var provider)
            && provider.ValueKind == JsonValueKind.String
                ? provider.GetString()
                : null;

        return new ReauthTokenValidationResult(
            IsValid: true,
            Uid: jsonWebToken.Subject,
            AuthTime: authTime,
            SignInProvider: signInProvider,
            Email: jsonWebToken.TryGetClaim("email", out var email) ? email.Value : null,
            HostedDomain: jsonWebToken.TryGetClaim("hd", out var hd) ? hd.Value : null);
    }
}
