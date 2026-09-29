using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Api.Middleware;

/// <summary>
/// Cross-tenant (IDOR) guard. Any authenticated request whose matched route
/// carries an <c>{organizationId}</c> value is denied with 403
/// <c>no_active_membership</c> unless the caller has an active membership in
/// that organization.
/// </summary>
/// <remarks>
/// <para>
/// Real middleware keyed off a routing convention, not a per-endpoint
/// filter, so a new organization-scoped route is covered without opting in.
/// It runs after the disabled-user gate and before <c>UseAuthorization()</c>,
/// so it needs <c>UseRouting()</c> to have matched the endpoint already.
/// </para>
/// <para>
/// Unauthenticated requests and routes without the value pass straight
/// through. A value that isn't an integer is denied, not ignored.
/// </para>
/// </remarks>
public sealed class OrganizationMembershipGuardMiddleware(RequestDelegate next)
{
    public const string NoActiveMembershipReason = "no_active_membership";

    public async Task InvokeAsync(HttpContext context, EvaluateOrganizationMembershipHandler membershipGuard)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !OrganizationRouteValues.HasOrganizationId(context)
            || context.User.GetIdentityPlatformUid() is not { } uid)
        {
            await next(context);
            return;
        }

        if (!OrganizationRouteValues.TryGetOrganizationId(context, out var organizationId)
            || await membershipGuard.HandleAsync(uid, organizationId, context.RequestAborted) == OrganizationAccessDecision.Deny)
        {
            await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "No active membership",
                "The caller has no active membership in the requested organization.",
                NoActiveMembershipReason));
            return;
        }

        await next(context);
    }
}
