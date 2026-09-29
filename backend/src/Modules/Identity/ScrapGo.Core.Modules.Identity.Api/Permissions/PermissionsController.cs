using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Api.Permissions;

public sealed record PermissionResponse(string Name);

/// <summary>
/// The permission catalog, read-only by design. It is a compile-time
/// contract seeded by migration, so there is deliberately no
/// POST/PUT/DELETE route here, and a spec asserts none exists.
/// </summary>
[ApiController]
[Authorize]
[Route("api/permissions")]
[Produces("application/json")]
public sealed class PermissionsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PermissionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] ListPermissionsHandler handler, CancellationToken cancellationToken) =>
        Ok((await handler.HandleAsync(cancellationToken)).Select(name => new PermissionResponse(name)));
}
