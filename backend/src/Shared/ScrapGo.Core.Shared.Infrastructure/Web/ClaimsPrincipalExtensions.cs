using System.Security.Claims;

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
}
