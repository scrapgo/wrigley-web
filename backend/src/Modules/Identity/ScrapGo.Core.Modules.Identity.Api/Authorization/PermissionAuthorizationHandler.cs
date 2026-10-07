using Microsoft.AspNetCore.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/>. It resolves the user from the
/// token's <c>sub</c> and the organization from the route, then asks
/// <see cref="PermissionResolver"/>. Organization context is never read from
/// token claims, so a token carrying a forged <c>organizationId</c> claim
/// changes nothing.
/// </summary>
public sealed class PermissionAuthorizationHandler(PermissionResolver permissionResolver, ICallerSignIn callerSignIn)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>Failure reason (and 400 <c>reason</c>) for an organization-scoped check on a route with no organization.</summary>
    public const string OrganizationContextRequiredReason = "organization_context_required";

    /// <summary>
    /// Failure reason (and 403 <c>reason</c>) for a platform-scoped check when
    /// the token isn't a Google Workspace sign-in on the internal allow-list.
    /// </summary>
    public const string WorkspaceSignInRequiredReason = "workspace_sign_in_required";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.GetIdentityPlatformUid() is not { } uid || context.Resource is not HttpContext httpContext)
        {
            return;
        }

        // Platform access needs a Workspace sign-in on every request, whatever
        // the caller's stored roles say. Named, so the client can say why.
        if (requirement.PlatformScope && !callerSignIn.IsInternalWorkspaceSignIn)
        {
            context.Fail(new AuthorizationFailureReason(this, WorkspaceSignInRequiredReason));
            return;
        }

        int? organizationId = null;
        int? applicationId = null;
        if (!requirement.PlatformScope)
        {
            if (!OrganizationRouteValues.TryGetOrganizationId(httpContext, out var routeOrganizationId))
            {
                context.Fail(new AuthorizationFailureReason(this, OrganizationContextRequiredReason));
                return;
            }

            organizationId = routeOrganizationId;

            // An application in the route makes the check application-scoped:
            // only that application's grants, and only its enabled modules.
            if (OrganizationRouteValues.TryGetApplicationId(httpContext, out var routeApplicationId))
            {
                applicationId = routeApplicationId;
            }
        }

        if (await permissionResolver.HasPermissionAsync(
                uid, organizationId, applicationId, requirement.PermissionName, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
