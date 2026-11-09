using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Mfa;

namespace ScrapGo.Core.Modules.Identity.Api.Session;

/// <param name="Code">The 6-digit code from the email; spaces and dashes are ignored.</param>
public sealed record VerifyMfaCodeRequest(string? Code);

/// <summary>
/// Email MFA for External users, when <c>Identity:RequireMfaForExternalUsers</c>
/// is on. Every other route answers 403 <c>mfa_required</c> until this
/// sign-in (the token's <c>auth_time</c>) has a verified code; these two are
/// reachable before that.
/// </summary>
[ApiController]
[Authorize]
[Route("api/session/mfa")]
[Produces("application/json")]
public sealed class MfaController(MfaService mfa) : ControllerBase
{
    /// <summary>Emails a new 6-digit code (valid 10 minutes), replacing any earlier one. At most one a minute, five per sign-in.</summary>
    [HttpPost("send")]
    [ProducesResponseType<MfaCodeSentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send(CancellationToken cancellationToken)
    {
        var result = await mfa.SendAsync(User.GetAuthTime(), cancellationToken);

        if (result is { Outcome: MfaOutcome.TooSoon, RetryAt: { } retryAt })
        {
            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling((retryAt - DateTimeOffset.UtcNow).TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return result is { Outcome: MfaOutcome.Success, Sent: { } sent } ? Ok(sent) : Problem(result.Outcome);
    }

    /// <summary>Checks the code. A right one verifies this sign-in (204); five wrong ones lock the code.</summary>
    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Verify([FromBody] VerifyMfaCodeRequest? request, CancellationToken cancellationToken)
    {
        var outcome = await mfa.VerifyAsync(User.GetAuthTime(), request?.Code, cancellationToken);

        return outcome is MfaOutcome.Success or MfaOutcome.AlreadyVerified ? NoContent() : Problem(outcome);
    }

    private static IActionResult Problem(MfaOutcome outcome) => outcome switch
    {
        MfaOutcome.NotRequired => Create(StatusCodes.Status409Conflict, "No code needed", "This sign-in doesn't need a code.", "mfa_not_required"),
        MfaOutcome.AlreadyVerified => Create(StatusCodes.Status409Conflict, "Already verified", "This sign-in is already verified.", "mfa_already_verified"),
        MfaOutcome.InvalidSession => Create(StatusCodes.Status400BadRequest, "Sign in again", "This session can't be verified. Sign in again.", "mfa_invalid_session"),
        MfaOutcome.TooSoon => Create(StatusCodes.Status429TooManyRequests, "Too soon", "A code was just sent. Wait a minute before asking for another.", "mfa_resend_too_soon"),
        MfaOutcome.SendLimitReached => Create(StatusCodes.Status429TooManyRequests, "Too many codes", "Too many codes were sent for this sign-in. Sign in again.", "mfa_send_limit_reached"),
        MfaOutcome.EmailUnavailable => Create(StatusCodes.Status503ServiceUnavailable, "Email unavailable", "The sign-in code couldn't be emailed. Try again later.", "mfa_email_unavailable"),
        MfaOutcome.NoChallenge => Create(StatusCodes.Status409Conflict, "No code sent", "Ask for a code first.", "mfa_no_code"),
        MfaOutcome.CodeExpired => Create(StatusCodes.Status409Conflict, "Code expired", "That code has expired. Send a new one.", "mfa_code_expired"),
        MfaOutcome.Locked => Create(StatusCodes.Status409Conflict, "Too many attempts", "Too many wrong codes. Send a new one.", "mfa_locked"),
        _ => Create(StatusCodes.Status400BadRequest, "Wrong code", "That code isn't right. Check the email and try again.", "mfa_invalid_code"),
    };

    private static IActionResult Create(int status, string title, string detail, string reason) =>
        ProblemResults.Create(status, title, detail, reason).ToActionResult();
}
