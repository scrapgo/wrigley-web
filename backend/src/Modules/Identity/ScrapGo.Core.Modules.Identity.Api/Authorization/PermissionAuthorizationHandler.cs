using Microsoft.AspNetCore.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/>. It resolves the user from the
/// token's <c>sub</c>, then asks <see cref="PermissionResolver"/>. Roles are
/// global, so there is no scope to read from the route.
/// </summary>
public sealed class PermissionAuthorizationHandler(PermissionResolver permissionResolver, ICallerSignIn callerSignIn)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// Failure reason (and 403 <c>reason</c>) when the token isn't a Google
    /// Workspace sign-in on the internal allow-list.
    /// </summary>
    public const string WorkspaceSignInRequiredReason = "workspace_sign_in_required";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.GetIdentityPlatformUid() is not { } uid || context.Resource is not HttpContext httpContext)
        {
            return;
        }

        // Every grant needs a Workspace sign-in on every request, whatever the
        // caller's stored roles say. Named, so the client can say why.
        if (!callerSignIn.IsInternalWorkspaceSignIn)
        {
            context.Fail(new AuthorizationFailureReason(this, WorkspaceSignInRequiredReason));
            return;
        }

        if (await permissionResolver.HasPermissionAsync(uid, requirement.PermissionName, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
