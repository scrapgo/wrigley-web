using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Invitations;

namespace ScrapGo.Core.Modules.Identity.Api.Invitations;

/// <param name="Token">The token from the invitation's accept link.</param>
public sealed record InvitationTokenRequest(string? Token);

/// <summary>
/// What the invite accept page calls before anyone has signed in. Anonymous:
/// the token is the credential. It goes in the body, never the URL, so it
/// isn't written to request logs.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/invitations")]
[Produces("application/json")]
public sealed class PublicInvitationsController(InvitationService invitations) : ControllerBase
{
    /// <summary>The invitation's status (Pending, Expired, Accepted, Revoked or NotFound) and a masked email.</summary>
    [HttpPost("lookup")]
    [ProducesResponseType<InvitationLookupDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup([FromBody] InvitationTokenRequest? request, CancellationToken cancellationToken) =>
        Ok(await invitations.LookupAsync(request?.Token, cancellationToken));

    /// <summary>
    /// Emails a new link for an expired external invitation to the invited
    /// address. The old link stops working. Returns 202 with no body: the new
    /// link only ever goes to the inbox.
    /// </summary>
    [HttpPost("renew")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Renew([FromBody] InvitationTokenRequest? request, CancellationToken cancellationToken) =>
        await invitations.RenewAsync(request?.Token, Request.PortalOrigin(), cancellationToken) switch
        {
            InvitationOutcome.Success => Accepted(),
            InvitationOutcome.RenewTooSoon => ProblemResults.Create(
                StatusCodes.Status429TooManyRequests, "Too soon",
                "A new link was just sent. Check your inbox, or try again in a minute.", "renew_too_soon").ToActionResult(),
            InvitationOutcome.RenewLimitReached => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Renewal limit reached",
                "This invitation can't be renewed again. Ask your administrator to resend it.", "renew_limit_reached").ToActionResult(),
            InvitationOutcome.NotRenewable => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Invitation can't be renewed",
                "Only an expired invitation can be renewed.", "invitation_not_renewable").ToActionResult(),
            _ => ControllerProblems.NotFound(),
        };
}
