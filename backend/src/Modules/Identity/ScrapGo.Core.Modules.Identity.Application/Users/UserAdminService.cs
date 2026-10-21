using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="RoleIds">The complete set of roles the user should hold; empty removes them all.</param>
public sealed record SetUserRolesCommand(int UserId, IReadOnlyList<int>? RoleIds);

/// <param name="SupplierRecordIds">The complete set of Quickbase supplier Record IDs; empty removes every link.</param>
public sealed record SetUserSuppliersCommand(int UserId, IReadOnlyList<int>? SupplierRecordIds);

public enum UserAdminOutcome
{
    /// <summary>Done, or already so (nothing written then).</summary>
    Success,

    /// <summary>A missing list, or a non-positive id in it.</summary>
    InvalidRequest,

    NotFound,

    /// <summary>A caller may not disable their own account.</summary>
    CannotDisableSelf,

    /// <summary>The change would leave no active Site Admin.</summary>
    LastSiteAdmin,

    /// <summary>One of the role ids doesn't exist.</summary>
    RoleNotFound,

    /// <summary>A role's user type differs from the user's: internal roles for Internal users, external for External.</summary>
    RoleTypeMismatch,

    /// <summary>Supplier links are for External users only; Internal users see every supplier.</summary>
    SupplierLinksExternalOnly,
}

/// <summary>
/// Admin changes to a user (ACCESS-MODEL.md section 9): status, roles and
/// supplier links. The routes require <c>users.manage</c>, checked before
/// these methods run.
/// </summary>
/// <remarks>
/// Each change commits with its audit row, then evicts the user's cached
/// access so it applies on their very next request. Anything that could
/// remove the last active Site Admin locks the <c>site_admin</c> role row
/// first, so two concurrent changes can't each see "another one remains".
/// </remarks>
public sealed class UserAdminService(
    IUserRepository users,
    IRoleRepository roles,
    ICurrentAccess currentAccess,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    /// <summary>Disables or enables the user. Idempotent.</summary>
    public async Task<UserAdminOutcome> ChangeStatusAsync(int userId, UserStatus status, CancellationToken cancellationToken)
    {
        var actorId = await ActorIdAsync(cancellationToken);
        if (status == UserStatus.Disabled && actorId == userId)
        {
            return UserAdminOutcome.CannotDisableSelf;
        }

        var (outcome, changed) = await unitOfWork.ExecuteInTransactionAsync<(UserAdminOutcome, bool)>(async ct =>
        {
            if (await users.GetByIdAsync(userId, ct) is not { } user)
            {
                return (UserAdminOutcome.NotFound, false);
            }

            if (user.Status == status)
            {
                return (UserAdminOutcome.Success, false);
            }

            if (status == UserStatus.Disabled && await IsLastActiveSiteAdminAsync(user, ct))
            {
                return (UserAdminOutcome.LastSiteAdmin, false);
            }

            await users.UpdateStatusAsync(userId, status, timeProvider.GetUtcNow(), ct);
            auditLog.Record(new AuditEvent(
                status == UserStatus.Disabled ? IdentityAuditEventTypes.UserDisabled : IdentityAuditEventTypes.UserEnabled,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new { targetUserId = userId })));
            await unitOfWork.SaveChangesAsync(ct);

            return (UserAdminOutcome.Success, true);
        }, cancellationToken);

        if (changed)
        {
            accessCache.EvictUser(userId);
        }

        return outcome;
    }

    /// <summary>Replaces the user's roles with exactly <see cref="SetUserRolesCommand.RoleIds"/>.</summary>
    public async Task<UserAdminOutcome> SetRolesAsync(SetUserRolesCommand command, CancellationToken cancellationToken)
    {
        if (command.RoleIds is not { } requested || requested.Any(id => id <= 0))
        {
            return UserAdminOutcome.InvalidRequest;
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var roleIds = requested.ToHashSet();

        var (outcome, changed) = await unitOfWork.ExecuteInTransactionAsync<(UserAdminOutcome, bool)>(async ct =>
        {
            if (await users.GetByIdAsync(command.UserId, ct) is not { } user)
            {
                return (UserAdminOutcome.NotFound, false);
            }

            var wanted = await roles.FindRolesAsync(roleIds, ct);
            if (wanted.Count != roleIds.Count)
            {
                return (UserAdminOutcome.RoleNotFound, false);
            }

            if (wanted.Any(role => role.UserType != user.UserType))
            {
                return (UserAdminOutcome.RoleTypeMismatch, false);
            }

            var current = await roles.ListUserRoleAssignmentsAsync(user.Id, ct);
            var toRemove = current.Where(assignment => !roleIds.Contains(assignment.RoleId)).ToList();
            var toAdd = wanted.Where(role => current.All(assignment => assignment.RoleId != role.Id)).ToList();
            if (toRemove.Count == 0 && toAdd.Count == 0)
            {
                return (UserAdminOutcome.Success, false);
            }

            var siteAdminRoleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.SiteAdmin, ct);
            if (toRemove.Any(assignment => assignment.RoleId == siteAdminRoleId) && await IsLastActiveSiteAdminAsync(user, ct))
            {
                return (UserAdminOutcome.LastSiteAdmin, false);
            }

            var now = timeProvider.GetUtcNow();
            foreach (var assignment in toRemove)
            {
                roles.RemoveUserRole(assignment);
            }

            foreach (var role in toAdd)
            {
                roles.AddUserRole(UserRole.Assign(user.Id, role.Id, role.UserType, actorId, now));
            }

            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.UserRolesChanged,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new
                {
                    targetUserId = user.Id,
                    added = toAdd.Select(role => role.Key).Order(),
                    removed = toRemove.Select(assignment => assignment.Role.Key).Order(),
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return (UserAdminOutcome.Success, true);
        }, cancellationToken);

        if (changed)
        {
            accessCache.EvictUser(command.UserId);
        }

        return outcome;
    }

    /// <summary>Replaces an External user's supplier links with exactly <see cref="SetUserSuppliersCommand.SupplierRecordIds"/>.</summary>
    public async Task<UserAdminOutcome> SetSuppliersAsync(SetUserSuppliersCommand command, CancellationToken cancellationToken)
    {
        if (command.SupplierRecordIds is not { } requested || requested.Any(id => id <= 0))
        {
            return UserAdminOutcome.InvalidRequest;
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var recordIds = requested.ToHashSet();

        var (outcome, changed) = await unitOfWork.ExecuteInTransactionAsync<(UserAdminOutcome, bool)>(async ct =>
        {
            if (await users.GetByIdAsync(command.UserId, ct) is not { } user)
            {
                return (UserAdminOutcome.NotFound, false);
            }

            if (user.UserType != UserType.External)
            {
                return (UserAdminOutcome.SupplierLinksExternalOnly, false);
            }

            var current = await users.ListSupplierLinksAsync(user.Id, ct);
            var toRemove = current.Where(link => !recordIds.Contains(link.SupplierRecordId)).ToList();
            var toAdd = recordIds.Where(id => current.All(link => link.SupplierRecordId != id)).Order().ToList();
            if (toRemove.Count == 0 && toAdd.Count == 0)
            {
                return (UserAdminOutcome.Success, false);
            }

            var now = timeProvider.GetUtcNow();
            users.RemoveSupplierLinks(toRemove);
            users.AddSupplierLinks(toAdd.Select(id => UserSupplier.Link(user.Id, id, actorId, now)));
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.UserSuppliersChanged,
                UserId: actorId,
                Metadata: JsonSerializer.Serialize(new
                {
                    targetUserId = user.Id,
                    added = toAdd,
                    removed = toRemove.Select(link => link.SupplierRecordId).Order(),
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return (UserAdminOutcome.Success, true);
        }, cancellationToken);

        if (changed)
        {
            accessCache.EvictUser(command.UserId);
        }

        return outcome;
    }

    /// <summary>
    /// True when the user is an active Site Admin and no other active user is.
    /// Locks the <c>site_admin</c> row first; must run inside the transaction.
    /// </summary>
    private async Task<bool> IsLastActiveSiteAdminAsync(User user, CancellationToken cancellationToken)
    {
        var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.SiteAdmin, cancellationToken);
        await roles.LockRoleAsync(roleId, cancellationToken);

        return user.IsActive
            && await roles.IsHeldByAsync(user.Id, roleId, cancellationToken)
            && await roles.CountActiveHoldersAsync(roleId, excludingUserId: user.Id, cancellationToken) == 0;
    }

    /// <summary>The route's permission check has already loaded the caller's access context.</summary>
    private async Task<int> ActorIdAsync(CancellationToken cancellationToken) =>
        (await currentAccess.GetAsync(cancellationToken))?.UserId
        ?? throw new InvalidOperationException("An authorized user-admin request has no access context.");
}
