using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Session;

namespace ScrapGo.Core.Modules.Identity.Api.Session;

/// <summary>
/// <c>POST /api/session</c>: called once after every sign-in (ACCESS-MODEL.md
/// section 7). Everything about the caller is read from the validated token;
/// there is no request body yet (invitation tokens arrive in B11).
/// </summary>
[ApiController]
[Authorize]
[Route("api/session")]
[Produces("application/json")]
public sealed class SessionController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Start([FromServices] StartSessionHandler handler, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var command = new StartSessionCommand(
            uid,
            Email: User.FindFirst("email")?.Value ?? string.Empty,
            EmailVerified: User.IsEmailVerified(),
            SignInProvider: User.GetSignInProvider(),
            HostedDomain: User.FindFirst("hd")?.Value,
            DisplayName: User.FindFirst("name")?.Value);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result switch
        {
            { Outcome: StartSessionOutcome.Started, Me: { } me } => Ok(me),
            { Outcome: StartSessionOutcome.AccountDisabled } => SessionProblems.AccountDisabled(),
            _ => SessionProblems.NoAccess(),
        };
    }
}
