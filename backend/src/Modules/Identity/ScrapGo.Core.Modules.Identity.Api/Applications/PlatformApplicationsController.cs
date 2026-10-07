using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Applications;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Applications;

/// <param name="ExpiresAt">Optional; the grant stops resolving at this instant (time-boxed access).</param>
public sealed record GrantApplicationRoleRequest(DateTimeOffset? ExpiresAt);

/// <summary>
/// Platform administration of an organization's applications: assign and
/// remove applications (<c>Application.Assign</c>), enable and disable their
/// licensed modules (<c>Module.Manage</c>), and grant or revoke application
/// roles from outside the organization, for example to appoint an application's
/// first administrator.
/// </summary>
/// <remarks>
/// Platform-scoped permissions only, and outside the membership guard
/// (<see cref="PlatformAdministrationAttribute"/>): a platform administrator
/// isn't a member of the organizations they administer.
/// </remarks>
[ApiController]
[Authorize]
[PlatformAdministration]
[Route("api/admin/organizations/{organizationId:int}/applications")]
[Produces("application/json")]
public sealed class PlatformApplicationsController(
    OrganizationApplicationService entitlements,
    ApplicationAccessService access) : ControllerBase
{
    /// <summary>
    /// The organization's assigned applications with their enabled modules, for
    /// platform admins who aren't members (the organization route needs membership).
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.AdminAccess, PlatformScope = true)]
    [ProducesResponseType<IReadOnlyList<OrganizationApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(int organizationId, CancellationToken cancellationToken) =>
        Ok(await entitlements.ListForOrganizationAsync(organizationId, cancellationToken));

    /// <summary>Assigns the application to the organization. Grants nobody anything. Idempotent.</summary>
    [HttpPut("{applicationId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationAssign, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapEntitlement(await entitlements.AssignAsync(uid, organizationId, applicationId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>
    /// Removes the application and hard-revokes every grant for it in the
    /// organization (each audited). Re-assigning restores no access. Idempotent.
    /// </summary>
    [HttpDelete("{applicationId:int}")]
    [RequirePermission(PermissionCatalog.ApplicationAssign, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Remove(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapEntitlement(await entitlements.RemoveAsync(uid, organizationId, applicationId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>Enables a licensed module on the organization's application. Idempotent.</summary>
    [HttpPut("{applicationId:int}/modules/{moduleId:int}")]
    [RequirePermission(PermissionCatalog.ModuleManage, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnableModule(int organizationId, int applicationId, int moduleId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapEntitlement(await entitlements.EnableModuleAsync(uid, organizationId, applicationId, moduleId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>Disables a module: its permissions stop resolving, grants are kept. Idempotent.</summary>
    [HttpDelete("{applicationId:int}/modules/{moduleId:int}")]
    [RequirePermission(PermissionCatalog.ModuleManage, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableModule(int organizationId, int applicationId, int moduleId, CancellationToken cancellationToken) =>
        User.GetIdentityPlatformUid() is { } uid
            ? MapEntitlement(await entitlements.DisableModuleAsync(uid, organizationId, applicationId, moduleId, cancellationToken))
            : ControllerProblems.MissingSubjectClaim();

    /// <summary>
    /// The application's roles usable in the organization (templates first, then
    /// its custom roles), so a platform admin can pick one to grant, e.g.
    /// "{App} Administrator" for the first application administrator.
    /// </summary>
    [HttpGet("{applicationId:int}/roles")]
    [RequirePermission(PermissionCatalog.AdminAccess, PlatformScope = true)]
    [ProducesResponseType<IReadOnlyList<RoleDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListRoles(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        Ok(await access.ListRolesAsync(organizationId, applicationId, cancellationToken));

    /// <summary>
    /// Grants an application role to a member (e.g. the first application
    /// administrator). No escalation guard: platform administrators act on the
    /// platform's behalf. Idempotent; a different <c>expiresAt</c> updates it.
    /// </summary>
    [HttpPut("{applicationId:int}/members/{userId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.AdminAccess, PlatformScope = true)]
    [RequirePermission(PermissionCatalog.RoleAssign, PlatformScope = true)]
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
            new ApplicationAccessCommand(uid, ActingAsPlatformAdmin: true, organizationId, applicationId, userId, roleId, request?.ExpiresAt),
            cancellationToken);

        return ApplicationProblems.Grant(this, result);
    }

    /// <summary>Revokes an application role. Not subject to the last-administrator rule. Idempotent.</summary>
    [HttpDelete("{applicationId:int}/members/{userId:int}/roles/{roleId:int}")]
    [RequirePermission(PermissionCatalog.AdminAccess, PlatformScope = true)]
    [RequirePermission(PermissionCatalog.RoleAssign, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        int organizationId, int applicationId, int userId, int roleId, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await access.RevokeAsync(
            new ApplicationAccessCommand(uid, ActingAsPlatformAdmin: true, organizationId, applicationId, userId, roleId), cancellationToken);

        return ApplicationProblems.Revoke(this, outcome);
    }

    private IActionResult MapEntitlement(EntitlementOutcome outcome) =>
        outcome switch
        {
            EntitlementOutcome.Changed or EntitlementOutcome.AlreadyInState => NoContent(),
            EntitlementOutcome.OrganizationNotFound => ControllerProblems.NotFound(),
            EntitlementOutcome.ApplicationNotFound => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Application not found",
                "No such application in the catalog, or it is retired.", "application_not_found").ToActionResult(),
            EntitlementOutcome.ModuleNotFound => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Module not found",
                "No such module of this application, or it is retired.", "module_not_found").ToActionResult(),
            EntitlementOutcome.ApplicationNotAssigned => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Application not assigned",
                "Assign the application to the organization before enabling its modules.", "application_not_assigned").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(EntitlementOutcome)}: {outcome}."),
        };
}
