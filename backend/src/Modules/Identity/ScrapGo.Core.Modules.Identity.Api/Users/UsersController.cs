using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Paging;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <summary>
/// Users: the caller's own record (<c>me</c>), and the platform-wide admin
/// reads. The caller's identity comes only from the validated bearer token's
/// <c>sub</c> claim, never from the request body or query. This controller
/// parses the request, calls one Application handler and maps the outcome; it
/// holds no business logic.
/// </summary>
/// <remarks>
/// <c>me</c> is open to any authenticated caller the disabled-user gate lets
/// through. The admin reads need <c>User.Read</c> at platform scope. Role
/// assignment carries its scope in the body or query (no
/// <c>{organizationId}</c> route value), so <see cref="UserRoleAssignmentService"/>
/// authorizes it per scope.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    /// <summary>Every user on the platform, paged by id. Filters: <c>search</c> (email substring), <c>status</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UserRead, PlatformScope = true)]
    [ProducesResponseType<PagedResult<UserSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] UserReadService userReads,
        CancellationToken cancellationToken)
    {
        var result = await userReads.ListAsync(new ListUsersQuery(search, status, page, pageSize), cancellationToken);

        return result switch
        {
            { Outcome: UserReadOutcome.Success, Users: { } users } => Ok(users),
            { Outcome: UserReadOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Invalid user list request",
                $"page must be 1 or more, pageSize 1 to {PageRequest.MaxPageSize}, and status one of: {string.Join(", ", Enum.GetNames<UserStatus>())}.",
                "invalid_request").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UserReadOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>One user and every role they hold, in every scope.</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCatalog.UserRead, PlatformScope = true)]
    [ProducesResponseType<UserDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, [FromServices] UserReadService userReads, CancellationToken cancellationToken) =>
        await userReads.GetAsync(id, cancellationToken) switch
        {
            { Outcome: UserReadOutcome.Success, User: { } user } => Ok(user),
            { Outcome: UserReadOutcome.NotFound } => ControllerProblems.NotFound(),
            var result => throw new InvalidOperationException($"Unhandled {nameof(UserReadOutcome)}: {result.Outcome}."),
        };

    /// <summary>
    /// Assigns a role to the user in one scope: <c>organizationId</c> in the
    /// body, or none for platform scope. Idempotent: assigning a role the user
    /// already holds there returns 200 without writing anything.
    /// </summary>
    [HttpPost("{id:int}/roles")]
    [ProducesResponseType<AssignedRoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        int id,
        AssignRoleRequest request,
        [FromServices] UserRoleAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var result = await assignments.AssignAsync(
            new AssignRoleCommand(uid, id, request.RoleId, request.OrganizationId), cancellationToken);

        return result switch
        {
            { Outcome: AssignRoleOutcome.Assigned or AssignRoleOutcome.AlreadyAssigned, Role: { } role } => Ok(role),
            { Outcome: AssignRoleOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role assignment",
                "roleId is required, and organizationId, if given, must be a positive id.", "invalid_request").ToActionResult(),
            { Outcome: AssignRoleOutcome.Forbidden } => ControllerProblems.MissingPermission(
                "Assigning a role in an organization requires membership and Role.Assign there."),
            { Outcome: AssignRoleOutcome.PlatformAdminRequired } => PlatformAdminRequired(),
            { Outcome: AssignRoleOutcome.UserNotFound } => ControllerProblems.NotFound(),
            // Deliberately identical: a role id from another organization is indistinguishable from an unknown one.
            { Outcome: AssignRoleOutcome.RoleNotFound or AssignRoleOutcome.RoleNotInOrganization } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Role not found",
                "No such role is available in this scope.", "role_not_found").ToActionResult(),
            { Outcome: AssignRoleOutcome.RoleScopeMismatch } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Role not assignable in this scope",
                "PlatformAdministrator is assigned only at platform scope, and OrganizationAdministrator only within an organization.",
                "role_scope_mismatch").ToActionResult(),
            { Outcome: AssignRoleOutcome.TargetNotMember } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "User is not a member",
                "The user has no active membership in this organization.", "user_not_a_member").ToActionResult(),
            { Outcome: AssignRoleOutcome.ExternalUserNotAllowed } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "External user not allowed",
                "External users can't hold platform roles. They may administer their own organization and applications.",
                "external_user_not_allowed").ToActionResult(),
            { Outcome: AssignRoleOutcome.CannotGrantUnheldPermission } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot grant unheld permission",
                "The role grants a permission you do not hold in this scope.", "cannot_grant_unheld_permission").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(AssignRoleOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>
    /// Revokes a role from the user in one scope: <c>?organizationId=</c>, or
    /// none for platform scope. Idempotent: revoking a role the user does not
    /// hold there returns 204 without writing anything.
    /// </summary>
    [HttpDelete("{id:int}/roles/{roleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeRole(
        int id,
        int roleId,
        [FromQuery] int? organizationId,
        [FromServices] UserRoleAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await assignments.RevokeAsync(new RevokeRoleCommand(uid, id, roleId, organizationId), cancellationToken);

        return outcome switch
        {
            RevokeRoleOutcome.Revoked or RevokeRoleOutcome.NotAssigned => NoContent(),
            RevokeRoleOutcome.InvalidRequest => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role revocation",
                "roleId and organizationId, if given, must be positive ids.", "invalid_request").ToActionResult(),
            RevokeRoleOutcome.Forbidden => ControllerProblems.MissingPermission(
                "Revoking a role in an organization requires membership and Role.Assign there."),
            RevokeRoleOutcome.PlatformAdminRequired => PlatformAdminRequired(),
            RevokeRoleOutcome.UserNotFound => ControllerProblems.NotFound(),
            RevokeRoleOutcome.LastPlatformAdministrator => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Last platform administrator",
                "This user is the only PlatformAdministrator. Assign another before revoking this one.",
                "last_platform_administrator").ToActionResult(),
            RevokeRoleOutcome.LastOrganizationAdministrator => ControllerProblems.LastOrganizationAdministrator(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RevokeRoleOutcome)}: {outcome}."),
        };
    }

    /// <summary>
    /// Disables the user: every later request of theirs is denied by the
    /// disabled-user gate, starting with the next one. Idempotent.
    /// </summary>
    [HttpPost("{id:int}/disable")]
    [RequirePermission(PermissionCatalog.UserUpdate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Disable(int id, [FromServices] UserStatusService userStatus, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, UserStatus.Disabled, userStatus, cancellationToken);

    /// <summary>Re-enables a disabled user. Idempotent.</summary>
    [HttpPost("{id:int}/enable")]
    [RequirePermission(PermissionCatalog.UserUpdate, PlatformScope = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Enable(int id, [FromServices] UserStatusService userStatus, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, UserStatus.Active, userStatus, cancellationToken);

    /// <summary>
    /// First-request auto-provisioning: returns the caller's user record,
    /// creating it on first sight, with their effective roles and permissions
    /// per scope.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(
        [FromServices] GetCurrentUserHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var command = new ProvisionCurrentUserCommand(
            uid,
            Email: User.FindFirst("email")?.Value ?? string.Empty,
            HostedDomain: User.FindFirst("hd")?.Value);

        return Ok(await handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Self-service secure account linking. <c>reauthIdToken</c> is validated
    /// independently and is never trusted to say which account it belongs to.
    /// </summary>
    [HttpPost("me/linked-providers/link")]
    [ProducesResponseType<LinkProviderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkProvider(
        LinkProviderRequest request,
        [FromServices] LinkProviderHandler handler,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        if (string.IsNullOrWhiteSpace(request.ReauthIdToken))
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest,
                "Missing reauthIdToken",
                "A fresh GCIP re-authentication ID token is required to link a provider.").ToActionResult();
        }

        var result = await handler.HandleAsync(new LinkProviderCommand(uid, request.ReauthIdToken), cancellationToken);

        return result.Outcome switch
        {
            LinkProviderOutcome.Linked => Ok(new LinkProviderResponse(Linked: true, result.Provider)),

            LinkProviderOutcome.InvalidReauthToken => ProblemResults.Create(
                StatusCodes.Status401Unauthorized,
                "Invalid re-authentication token",
                "The reauthIdToken failed signature/issuer/audience/lifetime validation.",
                "invalid_reauth_token").ToActionResult(),

            LinkProviderOutcome.ReauthIdentityMismatch => ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Re-authentication identity mismatch",
                "The reauthIdToken does not belong to the authenticated caller.",
                "reauth_identity_mismatch").ToActionResult(),

            LinkProviderOutcome.ReauthNotFresh => ProblemResults.Create(
                StatusCodes.Status403Forbidden,
                "Re-authentication not fresh",
                "The reauthIdToken's auth_time is outside the freshness window; re-authenticate again.",
                "reauth_not_fresh").ToActionResult(),

            LinkProviderOutcome.UserNotProvisioned => ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "User not provisioned",
                "Call GET /api/users/me before linking a provider.",
                "user_not_provisioned").ToActionResult(),

            _ => throw new InvalidOperationException($"Unhandled {nameof(LinkProviderOutcome)}: {result.Outcome}."),
        };
    }

    private async Task<IActionResult> ChangeStatusAsync(
        int id, UserStatus status, UserStatusService userStatus, CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await userStatus.ChangeAsync(new ChangeUserStatusCommand(uid, id, status), cancellationToken);

        return outcome switch
        {
            UserStatusOutcome.Changed or UserStatusOutcome.AlreadyInState => NoContent(),
            UserStatusOutcome.NotFound => ControllerProblems.NotFound(),
            UserStatusOutcome.CannotDisableSelf => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot disable yourself",
                "Another administrator must disable your account.", "cannot_disable_self").ToActionResult(),
            UserStatusOutcome.LastPlatformAdministrator => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Last platform administrator",
                "This user is the only active PlatformAdministrator. Enable or assign another before disabling this one.",
                "last_platform_administrator").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(UserStatusOutcome)}: {outcome}."),
        };
    }

    private static ObjectResult PlatformAdminRequired() =>
        ProblemResults.Create(
            StatusCodes.Status403Forbidden,
            "Platform administrator required",
            "Platform-scoped roles can only be assigned or revoked by a platform administrator.",
            "platform_admin_required").ToActionResult();
}
