using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <summary>
/// The caller's own user record. Identity comes only from the validated
/// bearer token's <c>sub</c> claim, never from the request body or query.
/// This controller parses the request, calls one Application handler and
/// maps the outcome; it holds no business logic.
/// </summary>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    /// <summary>
    /// First-request auto-provisioning: returns the caller's user record,
    /// creating it on first sight.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(
        [FromServices] ProvisionCurrentUserHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var command = new ProvisionCurrentUserCommand(
            uid,
            Email: User.FindFirst("email")?.Value ?? string.Empty,
            HostedDomain: User.FindFirst("hd")?.Value);

        return Ok(await handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Self-service secure account linking. <c>reauthIdToken</c> is validated
    /// independently and is never trusted to say which account it belongs to.
    /// </summary>
    [HttpPost("me/linked-providers/link")]
    [ProducesResponseType<LinkProviderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkProvider(
        LinkProviderRequest request,
        [FromServices] LinkProviderHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        if (string.IsNullOrWhiteSpace(request.ReauthIdToken))
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Missing reauthIdToken",
                "A fresh GCIP re-authentication ID token is required to link a provider.").ToActionResult();
        }

        var result = await handler.HandleAsync(new LinkProviderCommand(uid, request.ReauthIdToken), cancellationToken);

        return result.Outcome switch
        {
            LinkProviderOutcome.Linked => Ok(new LinkProviderResponse(Linked: true, result.Provider)),

            LinkProviderOutcome.InvalidReauthToken => ProblemResults.Create(
                StatusCodes.Status401Unauthorized,
                "Invalid re-authentication token",
                "The reauthIdToken failed signature/issuer/audience/lifetime validation.",
                "invalid_reauth_token").ToActionResult(),

            LinkProviderOutcome.ReauthIdentityMismatch => ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Re-authentication identity mismatch",
                "The reauthIdToken does not belong to the authenticated caller.",
                "reauth_identity_mismatch").ToActionResult(),

            LinkProviderOutcome.ReauthNotFresh => ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Re-authentication not fresh",
                "The reauthIdToken's auth_time is outside the freshness window; re-authenticate again.",
                "reauth_not_fresh").ToActionResult(),

            LinkProviderOutcome.UserNotProvisioned => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "User not provisioned",
                "Call GET /api/users/me before linking a provider.",
                "user_not_provisioned").ToActionResult(),

            _ => throw new InvalidOperationException($"Unhandled {nameof(LinkProviderOutcome)}: {result.Outcome}."),
        };
    }
}
