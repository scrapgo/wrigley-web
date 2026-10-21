using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Roles;

/// <param name="Permissions">Every permission key the role should grant; keys not listed are removed.</param>
public sealed record SetRolePermissionsRequest(IReadOnlyList<string>? Permissions);

/// <summary>
/// Roles (ACCESS-MODEL.md section 9). V1's roles are fixed: there is
/// deliberately no create or delete route, only changing what a role grants.
/// This controller parses, calls <see cref="RoleAdminService"/> and maps the outcome.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/roles")]
[Produces("application/json")]
public sealed class RolesController(RoleAdminService roles) : ControllerBase
{
    /// <summary>Every role, with the permission keys it grants and how many users hold it.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.RolesView)]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await roles.ListRolesAsync(cancellationToken));

    /// <summary>
    /// Sets the role's permissions to exactly <c>permissions</c>, subject to the
    /// audience rule. Applies to every holder within the access cache's 30
    /// seconds (immediately on this instance). Returns the updated role.
    /// </summary>
    [HttpPut("{id:int}/permissions")]
    [RequirePermission(PermissionCatalog.RolesManage)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(int id, [FromBody] SetRolePermissionsRequest? request, CancellationToken cancellationToken)
    {
        var result = await roles.SetPermissionsAsync(new SetRolePermissionsCommand(id, request?.Permissions), cancellationToken);

        return result switch
        {
            { Outcome: RoleAdminOutcome.Success, Role: { } role } => Ok(role),
            { Outcome: RoleAdminOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role permissions",
                "A list of permission keys is required (an empty list removes them all).", "invalid_request").ToActionResult(),
            { Outcome: RoleAdminOutcome.UnknownPermission } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Unknown permission",
                "Every key must be in the permission catalog (GET /api/admin/permissions).", "unknown_permission").ToActionResult(),
            { Outcome: RoleAdminOutcome.AudienceMismatch } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Audience mismatch",
                "Internal permissions go on internal roles and External permissions on external roles.", "audience_mismatch").ToActionResult(),
            _ => ControllerProblems.NotFound(),
        };
    }
}
