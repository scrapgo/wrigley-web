using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Turns the specific "organization-scoped permission on a route with no
/// organization" failure into a 400 <c>organization_context_required</c>
/// ProblemDetails. That is a wiring mistake, not a caller who lacks rights,
/// so it shouldn't be a bare 403. Every other outcome goes to the framework's
/// default handler unchanged.
/// </summary>
public sealed class OrganizationContextAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var missingOrganizationContext = authorizeResult is { Succeeded: false, Forbidden: true, AuthorizationFailure: { } failure }
            && failure.FailureReasons.Any(r => r.Message == PermissionAuthorizationHandler.OrganizationContextRequiredReason);

        return missingOrganizationContext
            ? ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Organization context required",
                "This action requires an organizationId route value, and none was present on the request.",
                PermissionAuthorizationHandler.OrganizationContextRequiredReason))
            : _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
