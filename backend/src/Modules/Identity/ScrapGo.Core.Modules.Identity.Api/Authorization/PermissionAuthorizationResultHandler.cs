using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ScrapGo.Core.Modules.Identity.Api.Authorization;

/// <summary>
/// Turns a permission check without a Google Workspace sign-in into 403
/// ProblemDetails with <c>reason: workspace_sign_in_required</c>, so the client
/// can ask the user to sign in with their Workspace account. Every other
/// outcome goes to the framework's default handler unchanged.
/// </summary>
public sealed class PermissionAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var workspaceSignInRequired = authorizeResult is { Succeeded: false, Forbidden: true, AuthorizationFailure: { } failure }
            && failure.FailureReasons.Any(r => r.Message == PermissionAuthorizationHandler.WorkspaceSignInRequiredReason);

        return workspaceSignInRequired
            ? ProblemResults.WriteAsync(context.Response, ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Workspace sign-in required",
                "Administration requires signing in with your Google Workspace account.",
                PermissionAuthorizationHandler.WorkspaceSignInRequiredReason))
            : _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
