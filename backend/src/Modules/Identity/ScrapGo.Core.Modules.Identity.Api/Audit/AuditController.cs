using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Audit;
using ScrapGo.Core.Shared.Kernel.Paging;
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Audit;

/// <summary>
/// The audit log (ACCESS-MODEL.md section 10), read-only: there is no write,
/// update or delete route. Needs <c>audit.view</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/audit")]
[Produces("application/json")]
public sealed class AuditController(AuditReadService audit) : ControllerBase
{
    /// <summary>
    /// Audit rows, newest first. Filters: <c>actor</c> (user id), <c>action</c>
    /// (exact event type), <c>target</c> (the user a change was made to),
    /// <c>from</c> (inclusive) and <c>to</c> (exclusive), as ISO 8601 instants.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.AuditView)]
    [ProducesResponseType<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int? actor,
        [FromQuery] string? action,
        [FromQuery] int? target,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        await audit.ListAsync(new ListAuditQuery(actor, action, target, from, to, page, pageSize), cancellationToken) is { } entries
            ? Ok(entries)
            : ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid audit request",
                $"page must be 1 or more, pageSize 1 to {PageRequest.MaxPageSize}, ids positive, and to after from.",
                "invalid_request").ToActionResult();
}
