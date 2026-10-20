using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Authorization;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="UserId">The user losing the role, from the route.</param>
public sealed record RevokeRoleCommand(string ActorUid, int UserId, int RoleId);

public enum AssignRoleOutcome
{
    Assigned,

    /// <summary>The user already held the role. Nothing was written.</summary>
    AlreadyAssigned,

    /// <summary>No role id, or a non-positive id.</summary>
    InvalidRequest,

    /// <summary>Only a platform administrator (<c>Admin.Access</c> and <c>Role.Assign</c>) may assign roles.</summary>
    PlatformAdminRequired,

    UserNotFound,

    /// <summary>No such active role.</summary>
    RoleNotFound,

    /// <summary>Escalation guard: the role grants a permission the caller doesn't hold.</summary>
    CannotGrantUnheldPermission,

    /// <summary>External users never hold administration roles.</summary>
    ExternalUserNotAllowed,
}

public sealed record AssignRoleResult(AssignRoleOutcome Outcome, AssignedRoleDto? Role = null);

public enum RevokeRoleOutcome
{
    Revoked,

    /// <summary>The user didn't hold the role. Nothing was written.</summary>
    NotAssigned,

    InvalidRequest,

    /// <inheritdoc cref="AssignRoleOutcome.PlatformAdminRequired"/>
    PlatformAdminRequired,

    UserNotFound,

    /// <summary>No other active user holds PlatformAdministrator; revoking it would lock everyone out of administration.</summary>
    LastPlatformAdministrator,
}

/// <summary>
/// Assigns roles to users and revokes them. Roles are global, and only a
/// platform administrator (<c>Admin.Access</c> and <c>Role.Assign</c>) may
/// change them.
/// </summary>
/// <remarks>
/// Every write runs in one transaction with its audit row, and invalidates
/// the target user's cached permissions, so the change applies on their next
/// request.
/// </remarks>
public sealed class UserRoleAssignmentService(
    IUserRepository users,
    IRoleRepository roles,
    PermissionResolver permissionResolver,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<AssignRoleResult> AssignAsync(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsWellFormed)
        {
            return new(AssignRoleOutcome.InvalidRequest);
        }

        var roleId = command.RoleId!.Value;

        if (await AuthorizeAsync(command.ActorUid, cancellationToken) is not { } actorUserId)
        {
            return new(AssignRoleOutcome.PlatformAdminRequired);
        }

        if (await users.GetByIdAsync(command.UserId, cancellationToken) is not { } target)
        {
            return new(AssignRoleOutcome.UserNotFound);
        }

        if (target.UserType == UserType.External)
        {
            return new(AssignRoleOutcome.ExternalUserNotAllowed);
        }

        if (await roles.FindActiveRoleAsync(roleId, cancellationToken) is not { } role)
        {
            return new(AssignRoleOutcome.RoleNotFound);
        }

        // Escalation guard: nobody hands out more than they hold.
        var held = await permissionResolver.GetPermissionNamesAsync(actorUserId, cancellationToken);
        var granted = await roles.GetRolePermissionsAsync(role.Id, cancellationToken);
        if (!granted.All(held.Contains))
        {
            return new(AssignRoleOutcome.CannotGrantUnheldPermission);
        }

        return await AssignInTransactionAsync(actorUserId, command.UserId, role, cancellationToken);
    }

    public async Task<RevokeRoleOutcome> RevokeAsync(RevokeRoleCommand command, CancellationToken cancellationToken)
    {
        if (command.RoleId <= 0)
        {
            return RevokeRoleOutcome.InvalidRequest;
        }

        if (await AuthorizeAsync(command.ActorUid, cancellationToken) is not { } actorUserId)
        {
            return RevokeRoleOutcome.PlatformAdminRequired;
        }

        if (await users.GetByIdAsync(command.UserId, cancellationToken) is null)
        {
            return RevokeRoleOutcome.UserNotFound;
        }

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await roles.FindUserRoleAsync(command.UserId, command.RoleId, ct) is not { } assignment)
            {
                return RevokeRoleOutcome.NotAssigned;
            }

            if (command.RoleId == await roles.GetPlatformRoleIdAsync(DefaultRoleNames.PlatformAdministrator, ct))
            {
                // Locked (as user disable does) so two concurrent changes can't
                // each see "another administrator remains" and remove the last
                // two together. Only active holders count: a disabled one can't act.
                await roles.LockRoleAsync(command.RoleId, ct);
                if (await roles.CountActiveHoldersAsync(command.RoleId, excludingUserId: command.UserId, ct) == 0)
                {
                    return RevokeRoleOutcome.LastPlatformAdministrator;
                }
            }

            roles.RemoveUserRole(assignment);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.RoleRevoked,
                UserId: actorUserId,
                Metadata: AuditMetadata(command.UserId, command.RoleId)));
            await unitOfWork.SaveChangesAsync(ct);

            return RevokeRoleOutcome.Revoked;
        }, cancellationToken);

        if (outcome == RevokeRoleOutcome.Revoked)
        {
            await permissionCache.InvalidateAsync(command.UserId, cancellationToken);
        }

        return outcome;
    }

    private async Task<AssignRoleResult> AssignInTransactionAsync(int actorUserId, int userId, Role role, CancellationToken cancellationToken)
    {
        var dto = new AssignedRoleDto(role.Id, role.Name);

        AssignRoleOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await roles.FindUserRoleAsync(userId, role.Id, ct) is not null)
                {
                    return AssignRoleOutcome.AlreadyAssigned;
                }

                roles.AddUserRole(UserRole.Assign(userId, role.Id, role.UserType, actorUserId, timeProvider.GetUtcNow()));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.RoleAssigned,
                    UserId: actorUserId,
                    Metadata: AuditMetadata(userId, role.Id)));
                await unitOfWork.SaveChangesAsync(ct);

                return AssignRoleOutcome.Assigned;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleAssignment)
        {
            // A concurrent request assigned it first; its audit row stands, ours rolled back.
            return new(AssignRoleOutcome.AlreadyAssigned, dto);
        }

        if (outcome == AssignRoleOutcome.Assigned)
        {
            await permissionCache.InvalidateAsync(userId, cancellationToken);
        }

        return new(outcome, dto);
    }

    /// <summary>The actor's user id when they hold <c>Admin.Access</c> and <c>Role.Assign</c>; null otherwise.</summary>
    private async Task<int?> AuthorizeAsync(string actorUid, CancellationToken cancellationToken) =>
        await users.GetIdByUidAsync(actorUid, cancellationToken) is { } actorUserId
        && await permissionResolver.HasPermissionAsync(actorUid, Permissions.AdminAccess, cancellationToken)
        && await permissionResolver.HasPermissionAsync(actorUid, Permissions.RoleAssign, cancellationToken)
            ? actorUserId
            : null;

    private static string AuditMetadata(int targetUserId, int roleId) =>
        JsonSerializer.Serialize(new { targetUserId, roleId });
}
