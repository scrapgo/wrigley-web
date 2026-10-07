using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Api.Middleware;

/// <summary>
/// Cross-tenant (IDOR) guard. Any authenticated request whose matched route
/// carries an <c>{organizationId}</c> value is denied with 403 unless the
/// caller has an active membership in that organization and the organization
/// is active:
/// <list type="bullet">
/// <item><c>no_active_membership</c>: not a member (or no such organization).</item>
/// <item><c>organization_deactivated</c>: a member of a deactivated organization.</item>
/// </list>
/// A route that also carries <c>{applicationId}</c> then needs the organization
/// to have that application: 404 <c>application_not_found</c> otherwise, so a
/// member can't tell an unassigned application from a nonexistent one.
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
/// through. A value that isn't an integer is denied, not ignored. Endpoints
/// marked <see cref="PlatformAdministrationAttribute"/> are skipped: they are
/// platform-scoped and carry their own platform permission requirement.
/// </para>
/// </remarks>
public sealed class OrganizationMembershipGuardMiddleware(RequestDelegate next)
{
    public const string NoActiveMembershipReason = "no_active_membership";

    public const string OrganizationDeactivatedReason = "organization_deactivated";

    public const string ApplicationNotFoundReason = "application_not_found";

    public async Task InvokeAsync(HttpContext context, EvaluateOrganizationMembershipHandler membershipGuard)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !OrganizationRouteValues.HasOrganizationId(context)
            || context.GetEndpoint()?.Metadata.GetMetadata<PlatformAdministrationAttribute>() is not null
            || context.User.GetIdentityPlatformUid() is not { } uid)
        {
            await next(context);
            return;
        }

        var decision = OrganizationRouteValues.TryGetOrganizationId(context, out var organizationId)
            ? await membershipGuard.HandleAsync(uid, organizationId, context.RequestAborted)
            : OrganizationAccessDecision.Deny;

        switch (decision)
        {
            case OrganizationAccessDecision.Allow:
                if (OrganizationRouteValues.HasApplicationId(context)
                    && !(OrganizationRouteValues.TryGetApplicationId(context, out var applicationId)
                        && await membershipGuard.HandleApplicationAsync(uid, organizationId, applicationId, context.RequestAborted)))
                {
                    await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                        StatusCodes.Status404NotFound,
                        "Application not found",
                        "This organization doesn't have that application.",
                        ApplicationNotFoundReason));
                    return;
                }

                await next(context);
                return;

            case OrganizationAccessDecision.DenyOrganizationDeactivated:
                await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                    StatusCodes.Status403Forbidden,
                    "Organization deactivated",
                    "This organization has been deactivated. Contact the platform administrator.",
                    OrganizationDeactivatedReason));
                return;

            default:
                await ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                    StatusCodes.Status403Forbidden,
                    "No active membership",
                    "The caller has no active membership in the requested organization.",
                    NoActiveMembershipReason));
                return;
        }
    }
}
