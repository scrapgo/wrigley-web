using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Application.Roles;

namespace ScrapGo.Core.Modules.Identity.Api.Roles;

public sealed record CreateRoleRequest(int? OrganizationId, string? Name, string? Description);

public sealed record UpdateRoleRequest(string? Name, string? Description);

public sealed record AttachPermissionRequest(string? PermissionName);

public sealed record AttachPermissionResponse(bool Attached, string Permission);

/// <summary>
/// Custom organization roles and their permissions.
/// </summary>
/// <remarks>
/// These routes carry no <c>{organizationId}</c>: organization context comes
/// from the request body (create) or the stored role (everything else). The
/// membership guard therefore can't cover them. Tenant isolation is enforced
/// inside <see cref="RoleService"/>, which requires an active membership in
/// the role's own organization and the matching Role.* permission there, and
/// applies the escalation guard.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/roles")]
[Produces("application/json")]
public sealed class RolesController(RoleService roleService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await roleService.CreateAsync(
            new CreateRoleCommand(uid, request.OrganizationId, request.Name, request.Description), cancellationToken);

        return result.Role is { } role && result.Outcome == RoleMutationOutcome.Success
            ? Created($"/api/roles/{role.Id}", role)
            : MutationProblem(result.Outcome, "organizationId and a non-blank name are required.",
                "Creating a role requires Role.Create in the target organization.");
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await roleService.UpdateAsync(new UpdateRoleCommand(uid, id, request.Name, request.Description), cancellationToken);

        return result.Role is { } role && result.Outcome == RoleMutationOutcome.Success
            ? Ok(role)
            : MutationProblem(result.Outcome, "A non-blank name is required.",
                "Editing a role requires Role.Update in its organization.");
    }

    /// <summary>Soft-deletes the role. Refused (409) while any user role still references it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        return await roleService.DeleteAsync(new DeleteRoleCommand(uid, id), cancellationToken) switch
        {
            RoleDeletionOutcome.Success => NoContent(),
            RoleDeletionOutcome.NotFound => ControllerProblems.NotFound(),
            RoleDeletionOutcome.Forbidden => ControllerProblems.MissingPermission(
                "Deleting a role requires Role.Delete in its organization."),
            RoleDeletionOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RoleDeletionOutcome.StillAssigned => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "Role is still assigned",
                "At least one user role still references this role; revoke every assignment before deleting it.",
                "role_still_assigned").ToActionResult(),
            var outcome => throw new InvalidOperationException($"Unhandled {nameof(RoleDeletionOutcome)}: {outcome}."),
        };
    }

    /// <summary>Attaches a catalog permission to the role. Idempotent.</summary>
    [HttpPost("{id:int}/permissions")]
    [ProducesResponseType<AttachPermissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachPermission(int id, AttachPermissionRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await roleService.AttachPermissionAsync(new RolePermissionCommand(uid, id, request.PermissionName), cancellationToken);

        return outcome == RolePermissionOutcome.Success
            ? Ok(new AttachPermissionResponse(Attached: true, request.PermissionName!))
            : PermissionProblem(outcome, "permissionName is not in the seeded permission catalog.");
    }

    /// <summary>Detaches a catalog permission from the role. Idempotent.</summary>
    [HttpDelete("{id:int}/permissions/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetachPermission(int id, string name, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await roleService.DetachPermissionAsync(new RolePermissionCommand(uid, id, name), cancellationToken);

        return outcome == RolePermissionOutcome.Success
            ? NoContent()
            : PermissionProblem(outcome, "This permission name is not in the seeded permission catalog.");
    }

    private static ObjectResult MutationProblem(RoleMutationOutcome outcome, string invalidDetail, string forbiddenDetail) =>
        outcome switch
        {
            RoleMutationOutcome.InvalidRequest => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role request", invalidDetail, "invalid_request").ToActionResult(),
            RoleMutationOutcome.Forbidden => ControllerProblems.MissingPermission(forbiddenDetail),
            RoleMutationOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RoleMutationOutcome.NotFound => ControllerProblems.NotFound(),
            RoleMutationOutcome.DuplicateName => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "Role name already exists",
                "This organization already has a role with this name.",
                "duplicate_role_name").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoleMutationOutcome)}: {outcome}."),
        };

    private static ObjectResult PermissionProblem(RolePermissionOutcome outcome, string unknownPermissionDetail) =>
        outcome switch
        {
            RolePermissionOutcome.RoleNotFound => ControllerProblems.NotFound(),
            RolePermissionOutcome.Forbidden => ControllerProblems.MissingPermission(
                "Changing a role's permissions requires Role.Update in its organization."),
            RolePermissionOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RolePermissionOutcome.CannotGrantUnheldPermission => ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Cannot grant unheld permission",
                "You may only attach permissions you hold yourself in this organization.",
                "cannot_grant_unheld_permission").ToActionResult(),
            RolePermissionOutcome.UnknownPermission => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Unknown permission", unknownPermissionDetail, "unknown_permission").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RolePermissionOutcome)}: {outcome}."),
        };
}
