using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Session;

namespace ScrapGo.Core.Modules.Identity.Api.Session;

/// <summary>
/// <c>GET /api/me</c>: the caller's user, roles, permissions and features.
/// Open to any authenticated caller the disabled-user gate lets through; it
/// never creates a user (<c>POST /api/session</c> does).
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
[Produces("application/json")]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get([FromServices] GetMeHandler handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(cancellationToken) is { } me ? Ok(me) : SessionProblems.NoAccess();
}
