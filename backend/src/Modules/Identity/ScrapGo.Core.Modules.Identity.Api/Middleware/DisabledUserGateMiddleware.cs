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

    public async Task InvokeAsync(HttpContext context, EvaluateUserStatusGateHandler gate)
    {
        // A missing uid on an authenticated principal is defensive only (GCIP
        // tokens always carry sub). It falls through so the anomaly surfaces
        // in the endpoint, not as a silent pass here.
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.GetIdentityPlatformUid() is { } uid
            && await gate.HandleAsync(uid, context.RequestAborted) == UserStatusGateDecision.Deny)
        {
            // No WWW-Authenticate header: RFC 6750 §3 governs bearer
            // challenges, and this is an application-level 403 against a
            // caller who already presented a valid token.
            await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "User disabled",
                "This account has been disabled.",
                DisabledUserReason));
            return;
        }

        await next(context);
    }
}
