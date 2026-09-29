using Microsoft.AspNetCore.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/>. It resolves the user from the
/// token's <c>sub</c> and the organization from the route, then asks
/// <see cref="PermissionResolver"/>. Organization context is never read from
/// token claims, so a token carrying a forged <c>organizationId</c> claim
/// changes nothing.
/// </summary>
public sealed class PermissionAuthorizationHandler(PermissionResolver permissionResolver)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>Failure reason (and 400 <c>reason</c>) for an organization-scoped check on a route with no organization.</summary>
    public const string OrganizationContextRequiredReason = "organization_context_required";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.GetIdentityPlatformUid() is not { } uid || context.Resource is not HttpContext httpContext)
        {
            return;
        }

        int? organizationId = null;
        if (!requirement.PlatformScope)
        {
            if (!OrganizationRouteValues.TryGetOrganizationId(httpContext, out var routeOrganizationId))
            {
                context.Fail(new AuthorizationFailureReason(this, OrganizationContextRequiredReason));
                return;
            }

            organizationId = routeOrganizationId;
        }

        if (await permissionResolver.HasPermissionAsync(uid, organizationId, requirement.PermissionName, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
