namespace ScrapGo.Core.Modules.Identity.Api.Middleware;

/// <summary>
/// Disabled-user gate. It runs between <c>UseAuthentication()</c> and
/// <c>UseAuthorization()</c>: after the caller's principal is established,
/// and before any endpoint or authorization policy runs.
/// </summary>
/// <remarks>
/// <para>
/// Real middleware rather than a per-endpoint filter, so every authenticated
/// route is covered without opting in. A route that forgot to reference a
/// filter would be silently exempt.
/// </para>
/// <para>
/// Unauthenticated requests pass straight through: <c>UseAuthorization()</c>
/// turns those into the ordinary 401. This gate only ever adds a 403, never
/// a 401, and never touches unprotected routes like <c>/healthz</c>.
/// </para>
/// </remarks>
public sealed class DisabledUserGateMiddleware(RequestDelegate next)
{
    /// <summary>The 403 <c>reason</c> extension member value.</summary>
    public const string DisabledUserReason = "user_disabled";

    /// <summary>The 403 <c>reason</c> when an external user's token lacks a second factor (MFA requirement on).</summary>
    public const string MfaRequiredReason = "mfa_required";

    public async Task InvokeAsync(HttpContext context, EvaluateUserStatusGateHandler gate)
    {
        // A missing uid on an authenticated principal is defensive only (GCIP
        // tokens always carry sub). It falls through so the anomaly surfaces
        // in the endpoint, not as a silent pass here.
        if (context.User.Identity?.IsAuthenticated == true && context.User.GetIdentityPlatformUid() is { } uid)
        {
            // No WWW-Authenticate header on these 403s: RFC 6750 §3 governs
            // bearer challenges, and this is an application-level 403 against
            // a caller who already presented a valid token.
            switch (await gate.HandleAsync(uid, HasSecondFactor(context.User), context.RequestAborted))
            {
                case UserStatusGateDecision.Deny:
                    await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                        StatusCodes.Status403Forbidden,
                        "User disabled",
                        "This account has been disabled.",
                        DisabledUserReason));
                    return;

                case UserStatusGateDecision.DenyMfaRequired:
                    await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                        StatusCodes.Status403Forbidden,
                        "Multi-factor authentication required",
                        "Sign in again with your second factor.",
                        MfaRequiredReason));
                    return;
            }
        }

        await next(context);
    }

    /// <summary>GCIP records a completed second factor as <c>firebase.sign_in_second_factor</c>.</summary>
    private static bool HasSecondFactor(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user.FindFirst("firebase")?.Value is not { Length: > 0 } firebase)
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(firebase);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("sign_in_second_factor", out var factor)
                && factor.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrEmpty(factor.GetString());
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
