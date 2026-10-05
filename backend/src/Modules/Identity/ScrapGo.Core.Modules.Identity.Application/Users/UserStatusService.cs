using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="ActorUid">The caller's UID from the validated token's <c>sub</c> claim.</param>
/// <param name="UserId">The user to enable or disable, from the route.</param>
/// <param name="Status">The status to move to, from the route (<c>enable</c> or <c>disable</c>).</param>
public sealed record ChangeUserStatusCommand(string ActorUid, int UserId, UserStatus Status);

public enum UserStatusOutcome
{
    Changed,

    /// <summary>The user was already in that status. Nothing was written.</summary>
    AlreadyInState,

    NotFound,

    /// <summary>A caller may not disable their own account.</summary>
    CannotDisableSelf,

    /// <summary>The user is the only active PlatformAdministrator; disabling them would lock everyone out of platform administration.</summary>
    LastPlatformAdministrator,
}

/// <summary>
/// Enables and disables users platform-wide, wrapping <see cref="User.Enable"/>
/// and <see cref="User.Disable"/>.
/// </summary>
/// <remarks>
/// <para>
/// The routes require <c>User.Update</c> at platform scope, checked before
/// this runs. Each change is one transaction with its audit row.
/// </para>
/// <para>
/// A disable takes effect on the user's next request: the disabled-user gate
/// reads the status from the database on every request, with no cache to
/// invalidate. Identity Platform is not touched; no custom claim mirrors this
/// status.
/// </para>
/// </remarks>
public sealed class UserStatusService(
    IUserRepository users,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<UserStatusOutcome> ChangeAsync(ChangeUserStatusCommand command, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);

        if (command.Status == UserStatus.Disabled && actorUserId == command.UserId)
        {
            return UserStatusOutcome.CannotDisableSelf;
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await users.GetByIdAsync(command.UserId, ct) is not { } user)
            {
                return UserStatusOutcome.NotFound;
            }

            if (user.Status == command.Status)
            {
                return UserStatusOutcome.AlreadyInState;
            }

            if (command.Status == UserStatus.Disabled && await IsLastActivePlatformAdministratorAsync(command.UserId, ct))
            {
                return UserStatusOutcome.LastPlatformAdministrator;
            }

            await users.UpdateStatusAsync(command.UserId, command.Status, timeProvider.GetUtcNow(), ct);
            auditLog.Record(new AuditEvent(
                command.Status == UserStatus.Disabled ? IdentityAuditEventTypes.UserDisabled : IdentityAuditEventTypes.UserEnabled,
                UserId: actorUserId,
                Metadata: JsonSerializer.Serialize(new { targetUserId = command.UserId })));
            await unitOfWork.SaveChangesAsync(ct);

            return UserStatusOutcome.Changed;
        }, cancellationToken);
    }

    /// <summary>
    /// True when the user holds PlatformAdministrator at platform scope and no
    /// other active user does. The role row is locked first (as revoking it
    /// does), so concurrent disables and revocations can't remove the last two
    /// administrators together.
    /// </summary>
    private async Task<bool> IsLastActivePlatformAdministratorAsync(int userId, CancellationToken cancellationToken)
    {
        var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.PlatformAdministrator, cancellationToken);
        if (!await roles.IsHeldByAsync(userId, roleId, organizationId: null, cancellationToken))
        {
            return false;
        }

        await roles.LockRoleAsync(roleId, cancellationToken);

        return await roles.CountActivePlatformHoldersAsync(roleId, excludingUserId: userId, cancellationToken) == 0;
    }
}
