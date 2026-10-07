using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Api.Roles;
using ScrapGo.Core.Modules.Identity.Application.Applications;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Applications;

/// <summary>
/// An organization's applications, and access within each one: its roles
/// (platform templates plus the organization's custom roles), who holds them,
/// and access review.
/// </summary>
/// <remarks>
/// <para>
/// Every route carries <c>{organizationId}</c>, so the membership guard covers
/// it. Routes that also carry <c>{applicationId}</c> are application-scoped:
/// the guard answers 404 <c>application_not_found</c> if the organization
/// doesn't have the application, and permissions resolve within that
/// application (its enabled modules only).
/// </para>
/// <para>
/// Access is managed only by application administrators
/// (<c>Application.ManageAccess</c> at application scope, ORG-APP-MODULE-MODEL.md
/// Decision 3). Organization administrators can see which applications their
/// organization has, but can't grant application roles.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:int}/applications")]
[Produces("application/json")]
public sealed class OrganizationApplicationsController(
    OrganizationApplicationService entitlements,
    ApplicationAccessService access) : ControllerBase
{
    /// <summary>The organization's applications and their enabled modules.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UserRead)]
    [ProducesResponseType<IReadOnlyList<OrganizationApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(int organizationId, CancellationToken cancellationToken) =>
        Ok(await entitlements.ListForOrganizationAsync(organizationId, cancellationToken));

    /// <summary>The application's roles usable here: platform templates first, then this organization's custom roles.</summary>
    [HttpGet("{applicationId:int}/roles")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<IReadOnlyList<RoleDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListRoles(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        Ok(await access.ListRolesAsync(organizationId, applicationId, cancellationToken));

    /// <summary>Creates a custom application role for this organization. Compose it with the permissions endpoints.</summary>
    [HttpPost("{applicationId:int}/roles")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole(
        int organizationId, int applicationId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await access.CreateRoleAsync(uid, organizationId, applicationId, request.Name, request.Description, cancellationToken);

        return result is { Outcome: RoleMutationOutcome.Success, Role: { } role }
            ? Created($"/api/organizations/{organizationId}/applications/{applicationId}/roles", role)
            : RoleMutationProblem(result.Outcome);
    }

    [HttpPut("{applicationId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(
        int organizationId, int applicationId, int roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await access.UpdateRoleAsync(uid, organizationId, applicationId, roleId, request.Name, request.Description, cancellationToken);

        return result is { Outcome: RoleMutationOutcome.Success, Role: { } role } ? Ok(role) : RoleMutationProblem(result.Outcome);
    }

    [HttpDelete("{applicationId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRole(int organizationId, int applicationId, int roleId, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        return await access.DeleteRoleAsync(uid, organizationId, applicationId, roleId, cancellationToken) switch
        {
            RoleDeletionOutcome.Success => NoContent(),
            RoleDeletionOutcome.NotFound => ControllerProblems.NotFound(),
            RoleDeletionOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RoleDeletionOutcome.StillAssigned => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Role is still assigned",
                "At least one user still holds this role; revoke every grant before deleting it.", "role_still_assigned").ToActionResult(),
            var outcome => throw new InvalidOperationException($"Unhandled {nameof(RoleDeletionOutcome)}: {outcome}."),
        };
    }

    /// <summary>Attaches one of this application's permissions to a custom role. Idempotent.</summary>
    [HttpPost("{applicationId:int}/roles/{roleId:int}/permissions")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<AttachPermissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachPermission(
        int organizationId, int applicationId, int roleId, AttachPermissionRequest request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await access.AttachPermissionAsync(uid, organizationId, applicationId, roleId, request.PermissionName, cancellationToken);

        return outcome == RolePermissionOutcome.Success
            ? Ok(new AttachPermissionResponse(Attached: true, request.PermissionName!))
            : PermissionProblem(outcome);
    }

    /// <summary>Detaches a permission from a custom role. Idempotent.</summary>
    [HttpDelete("{applicationId:int}/roles/{roleId:int}/permissions/{name}")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetachPermission(
        int organizationId, int applicationId, int roleId, string name, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await access.DetachPermissionAsync(uid, organizationId, applicationId, roleId, name, cancellationToken);

        return outcome == RolePermissionOutcome.Success ? NoContent() : PermissionProblem(outcome);
    }

    /// <summary>
    /// Grants an application role to a member. The escalation guard applies:
    /// only roles whose permissions you hold here. Idempotent; a different
    /// <c>expiresAt</c> updates the grant.
    /// </summary>
    [HttpPut("{applicationId:int}/members/{userId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<AssignedRoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Grant(
        int organizationId, int applicationId, int userId, int roleId, GrantApplicationRoleRequest? request, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await access.GrantAsync(
            new ApplicationAccessCommand(uid, ActingAsPlatformAdmin: false, organizationId, applicationId, userId, roleId, request?.ExpiresAt),
            cancellationToken);

        return ApplicationProblems.Grant(this, result);
    }

    /// <summary>Revokes an application role. The application's last active administrator can't be revoked. Idempotent.</summary>
    [HttpDelete("{applicationId:int}/members/{userId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        int organizationId, int applicationId, int userId, int roleId, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await access.RevokeAsync(
            new ApplicationAccessCommand(uid, ActingAsPlatformAdmin: false, organizationId, applicationId, userId, roleId), cancellationToken);

        return ApplicationProblems.Revoke(this, outcome);
    }

    /// <summary>Access review: every member with access to the application here, their roles and what they resolve to now.</summary>
    [HttpGet("{applicationId:int}/access")]
    [RequirePermission(PermissionCatalog.ApplicationManageAccess)]
    [ProducesResponseType<IReadOnlyList<AccessReviewEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewAccess(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        Ok(await access.ReviewAccessAsync(organizationId, applicationId, cancellationToken));

    private static ObjectResult RoleMutationProblem(RoleMutationOutcome outcome) =>
        outcome switch
        {
            RoleMutationOutcome.InvalidRequest => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role request", "A non-blank name is required.", "invalid_request").ToActionResult(),
            RoleMutationOutcome.NotFound => ControllerProblems.NotFound(),
            RoleMutationOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RoleMutationOutcome.DuplicateName => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Role name already exists",
                "This organization already has a role with this name.", "duplicate_role_name").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RoleMutationOutcome)}: {outcome}."),
        };

    private static ObjectResult PermissionProblem(RolePermissionOutcome outcome) =>
        outcome switch
        {
            RolePermissionOutcome.RoleNotFound => ControllerProblems.NotFound(),
            RolePermissionOutcome.UnknownPermission => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Unknown permission",
                "That permission isn't one of this application's.", "unknown_permission").ToActionResult(),
            RolePermissionOutcome.CannotModifyOwnRole => ControllerProblems.CannotModifyOwnRole(),
            RolePermissionOutcome.CannotGrantUnheldPermission => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot grant unheld permission",
                "You may only attach permissions you hold yourself in this application.", "cannot_grant_unheld_permission").ToActionResult(),
            RolePermissionOutcome.Forbidden => ControllerProblems.MissingPermission("Managing this application's roles requires Application.ManageAccess."),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RolePermissionOutcome)}: {outcome}."),
        };
}
