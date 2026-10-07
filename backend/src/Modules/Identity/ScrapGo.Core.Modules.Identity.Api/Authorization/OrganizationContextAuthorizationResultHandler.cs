using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Turns two specific authorization failures into ProblemDetails with a
/// <c>reason</c>:
/// <list type="bullet">
/// <item>An organization-scoped permission on a route with no organization:
/// 400 <c>organization_context_required</c>. That is a wiring mistake, not a
/// caller who lacks rights.</item>
/// <item>A platform-scoped permission without a Google Workspace sign-in:
/// 403 <c>workspace_sign_in_required</c>, so the client can ask the user to
/// sign in with their Workspace account.</item>
/// </list>
/// Every other outcome goes to the framework's default handler unchanged.
/// </summary>
public sealed class OrganizationContextAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var reasons = authorizeResult is { Succeeded: false, Forbidden: true, AuthorizationFailure: { } failure }
            ? failure.FailureReasons.Select(r => r.Message).ToHashSet(StringComparer.Ordinal)
            : [];

        if (reasons.Contains(PermissionAuthorizationHandler.OrganizationContextRequiredReason))
        {
            return ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Organization context required",
                "This action requires an organizationId route value, and none was present on the request.",
                PermissionAuthorizationHandler.OrganizationContextRequiredReason));
        }

        if (reasons.Contains(PermissionAuthorizationHandler.WorkspaceSignInRequiredReason))
        {
            return ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Workspace sign-in required",
                "Platform administration requires signing in with your Google Workspace account.",
                PermissionAuthorizationHandler.WorkspaceSignInRequiredReason));
        }

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
