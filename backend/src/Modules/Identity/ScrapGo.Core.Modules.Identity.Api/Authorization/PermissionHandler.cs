using Microsoft.AspNetCore.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Access;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> against the caller's
/// <see cref="AccessContext"/> (through <see cref="ICurrentAccess"/>, so at
/// most one load per request). Permissions come from the database, never from
/// token claims.
/// </summary>
public sealed class PermissionHandler(ICurrentAccess currentAccess) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var cancellationToken = context.Resource is HttpContext httpContext ? httpContext.RequestAborted : CancellationToken.None;

        if (await currentAccess.GetAsync(cancellationToken) is { } access && requirement.AnyOf.Any(access.Can))
        {
            context.Succeed(requirement);
        }
    }
}
