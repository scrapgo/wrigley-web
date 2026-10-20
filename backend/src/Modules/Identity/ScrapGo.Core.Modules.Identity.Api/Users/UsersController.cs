using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrapGo.Core.Modules.Identity.Api.Authorization;
using ScrapGo.Core.Modules.Identity.Domain.Users;
using ScrapGo.Core.Shared.Kernel.Paging;
// Api.Permissions (the catalog controller's namespace) would otherwise shadow the domain catalog.
using PermissionCatalog = ScrapGo.Core.Modules.Identity.Domain.Authorization.Permissions;

namespace ScrapGo.Core.Modules.Identity.Api.Users;

/// <summary>
/// Users: the platform-wide admin reads and changes. The caller's identity
/// comes only from the validated bearer token's <c>sub</c> claim, never from
/// the request body or query. This controller
/// parses the request, calls one Application handler and maps the outcome; it
/// holds no business logic.
/// </summary>
/// <remarks>
/// The caller's own record is <c>GET /api/me</c>. The admin reads need <c>User.Read</c>. Role assignment is
/// authorized inside <see cref="UserRoleAssignmentService"/>.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    /// <summary>Every user on the platform, paged by id. Filters: <c>search</c> (email substring), <c>status</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.UsersView)]
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

    /// <summary>One user and every role they hold.</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCatalog.UsersView)]
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
    /// Assigns a role to the user. Idempotent: assigning a role the user
    /// already holds returns 200 without writing anything.
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
            new AssignRoleCommand(uid, id, request.RoleId), cancellationToken);

        return result switch
        {
            { Outcome: AssignRoleOutcome.Assigned or AssignRoleOutcome.AlreadyAssigned, Role: { } role } => Ok(role),
            { Outcome: AssignRoleOutcome.InvalidRequest } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role assignment",
                "roleId is required and must be a positive id.", "invalid_request").ToActionResult(),
            { Outcome: AssignRoleOutcome.PlatformAdminRequired } => PlatformAdminRequired(),
            { Outcome: AssignRoleOutcome.UserNotFound } => ControllerProblems.NotFound(),
            { Outcome: AssignRoleOutcome.RoleNotFound } => ProblemResults.Create(
                StatusCodes.Status404NotFound, "Role not found", "No such role.", "role_not_found").ToActionResult(),
            { Outcome: AssignRoleOutcome.ExternalUserNotAllowed } => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "External user not allowed",
                "External users can't hold administration roles.", "external_user_not_allowed").ToActionResult(),
            { Outcome: AssignRoleOutcome.CannotGrantUnheldPermission } => ProblemResults.Create(
                StatusCodes.Status403Forbidden, "Cannot grant unheld permission",
                "The role grants a permission you do not hold.", "cannot_grant_unheld_permission").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(AssignRoleOutcome)}: {result.Outcome}."),
        };
    }

    /// <summary>
    /// Revokes a role from the user. Idempotent: revoking a role the user does
    /// not hold returns 204 without writing anything.
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
        [FromServices] UserRoleAssignmentService assignments,
        CancellationToken cancellationToken)
    {
        if (User.GetIdentityPlatformUid() is not { } uid)
        {
            return ControllerProblems.MissingSubjectClaim();
        }

        var outcome = await assignments.RevokeAsync(new RevokeRoleCommand(uid, id, roleId), cancellationToken);

        return outcome switch
        {
            RevokeRoleOutcome.Revoked or RevokeRoleOutcome.NotAssigned => NoContent(),
            RevokeRoleOutcome.InvalidRequest => ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Invalid role revocation",
                "roleId must be a positive id.", "invalid_request").ToActionResult(),
            RevokeRoleOutcome.PlatformAdminRequired => PlatformAdminRequired(),
            RevokeRoleOutcome.UserNotFound => ControllerProblems.NotFound(),
            RevokeRoleOutcome.LastPlatformAdministrator => ProblemResults.Create(
                StatusCodes.Status409Conflict, "Last platform administrator",
                "This user is the only PlatformAdministrator. Assign another before revoking this one.",
                "last_platform_administrator").ToActionResult(),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RevokeRoleOutcome)}: {outcome}."),
        };
    }

    /// <summary>
    /// Disables the user: every later request of theirs is denied by the
    /// disabled-user gate, starting with the next one. Idempotent.
    /// </summary>
    [HttpPost("{id:int}/disable")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Disable(int id, [FromServices] UserStatusService userStatus, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, UserStatus.Disabled, userStatus, cancellationToken);

    /// <summary>Re-enables a disabled user. Idempotent.</summary>
    [HttpPost("{id:int}/enable")]
    [RequirePermission(PermissionCatalog.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Enable(int id, [FromServices] UserStatusService userStatus, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, UserStatus.Active, userStatus, cancellationToken);

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
            "Roles can only be assigned or revoked by a platform administrator.",
            "platform_admin_required").ToActionResult();
}
