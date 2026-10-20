using System.Security.Claims;
using System.Text.Json;

namespace ScrapGo.Core.Shared.Infrastructure.Web;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The caller's GCIP UID from the validated token's <c>sub</c> claim, the
    /// only identity key this platform resolves users by. Never read identity
    /// from a request body or query string.
    /// </summary>
    public static string? GetIdentityPlatformUid(this ClaimsPrincipal principal)
    {
        var uid = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(uid) ? null : uid;
    }

    /// <summary>True only when the token's <c>email_verified</c> claim is <c>true</c>.</summary>
    public static bool IsEmailVerified(this ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirstValue("email_verified"), out var verified) && verified;

    /// <summary>GCIP's <c>firebase.sign_in_provider</c>, e.g. <c>google.com</c> or <c>password</c>; null when absent.</summary>
    public static string? GetSignInProvider(this ClaimsPrincipal principal) =>
        principal.GetFirebaseClaim("sign_in_provider");

    /// <summary>GCIP's <c>firebase.sign_in_second_factor</c>; null when the sign-in had no second factor.</summary>
    public static string? GetSignInSecondFactor(this ClaimsPrincipal principal) =>
        principal.GetFirebaseClaim("sign_in_second_factor");

    /// <summary>One string member of GCIP's <c>firebase</c> JSON claim, or null.</summary>
    private static string? GetFirebaseClaim(this ClaimsPrincipal principal, string member)
    {
        if (principal.FindFirstValue("firebase") is not { Length: > 0 } firebase)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(firebase);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(member, out var value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { Length: > 0 } text
                    ? text
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
