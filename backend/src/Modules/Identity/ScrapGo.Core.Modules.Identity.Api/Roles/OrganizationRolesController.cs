using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Api.Permissions;
using ScrapGo.Core.Modules.Identity.Application.Roles;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Roles;

/// <summary>
/// Read-only role endpoints for one organization: its own custom roles plus
/// the built-in platform-defined roles (<c>organizationId</c> null in the body).
/// </summary>
/// <remarks>
/// Organization-scoped by route, so the membership guard covers every action
/// (403 <c>no_active_membership</c>), and each action needs <c>Role.Read</c>
/// in that organization. A role that isn't visible from the route's
/// organization, including another organization's, is 404, never 403.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}/roles")]
[Produces("application/json")]
public sealed class OrganizationRolesController(RoleReadService roleReads) : ControllerBase
{
    /// <summary>Lists the organization's active roles and the built-ins, built-ins first, then by name.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.RoleRead)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(int organizationId, CancellationToken cancellationToken) =>
        Ok(await roleReads.ListAsync(organizationId, cancellationToken));

    /// <summary>One role and the permission names it grants.</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCatalog.RoleRead)]
    [ProducesResponseType<RoleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int organizationId, int id, CancellationToken cancellationToken) =>
        await roleReads.GetAsync(organizationId, id, cancellationToken) switch
        {
            { Outcome: RoleReadOutcome.Success, Role: { } role } => Ok(role),
            { Outcome: RoleReadOutcome.NotFound } => ControllerProblems.NotFound(),
            var result => throw new InvalidOperationException($"Unhandled {nameof(RoleReadOutcome)}: {result.Outcome}."),
        };

    /// <summary>The permissions the role grants, ordered by name, in the same shape as <c>GET /api/permissions</c>.</summary>
    [HttpGet("{id:int}/permissions")]
    [RequirePermission(PermissionCatalog.RoleRead)]
    [ProducesResponseType<IReadOnlyList<PermissionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(int organizationId, int id, CancellationToken cancellationToken) =>
        await roleReads.GetPermissionsAsync(organizationId, id, cancellationToken) switch
        {
            { Outcome: RoleReadOutcome.Success, Permissions: { } names } => Ok(names.Select(name => new PermissionResponse(name))),
            { Outcome: RoleReadOutcome.NotFound } => ControllerProblems.NotFound(),
            var result => throw new InvalidOperationException($"Unhandled {nameof(RoleReadOutcome)}: {result.Outcome}."),
        };
}
