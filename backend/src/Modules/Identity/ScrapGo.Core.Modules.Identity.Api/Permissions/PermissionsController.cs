using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Permissions;

/// <summary>
/// The permission catalog, grouped by feature. Read-only by design: the keys
/// are a compile-time contract seeded by migration, so there is deliberately
/// no write route here, and a spec asserts none exists. Mapping keys to roles
/// is <c>PUT /api/admin/roles/{id}/permissions</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/permissions")]
[Produces("application/json")]
public sealed class PermissionsController(RoleAdminService roles) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.RolesView)]
    [ProducesResponseType<IReadOnlyList<PermissionGroupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await roles.ListPermissionsAsync(cancellationToken));
}
