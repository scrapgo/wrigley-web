using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <param name="IdentityPlatformUid">The GCIP UID of an existing Internal user.</param>
public sealed record BootstrapSiteAdminCommand(string IdentityPlatformUid);

public enum BootstrapSiteAdminOutcome
{
    /// <summary>The user now holds <see cref="DefaultRoleNames.SiteAdmin"/>.</summary>
    Granted,

    /// <summary>The user already held it; nothing was written.</summary>
    AlreadyGranted,

    /// <summary>No user row for the UID. They must sign in (<c>POST /api/session</c>) once first.</summary>
    UserNotProvisioned,

    /// <summary>Another active user already holds it. Later grants go through the role-assignment API.</summary>
    ActiveSiteAdminExists,

    /// <summary>The user is External. Site Admin is an internal role.</summary>
    ExternalUserNotAllowed,
}

/// <summary>
/// Assigns <see cref="DefaultRoleNames.SiteAdmin"/> to an existing Internal
/// user while no active Site Admin exists: how a database gets its first
/// administrator, or recovers when every Site Admin is disabled. It runs from
/// the host's <c>bootstrap-site-admin</c> command only, never over HTTP and
/// never at sign-in.
/// </summary>
/// <remarks>
/// Idempotent for the same UID. The role row is locked for the
/// check-then-assign, so concurrent runs serialize. The grant is audited as a
/// system action.
/// </remarks>
public sealed class BootstrapSiteAdminHandler(
    IUserRepository users,
    IRoleRepository roles,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<BootstrapSiteAdminOutcome> HandleAsync(BootstrapSiteAdminCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdentityPlatformUid)
            || await users.GetIdByUidAsync(command.IdentityPlatformUid, cancellationToken) is not { } userId)
        {
            return BootstrapSiteAdminOutcome.UserNotProvisioned;
        }

        if (await users.GetByIdAsync(userId, cancellationToken) is not { UserType: UserType.Internal })
        {
            return BootstrapSiteAdminOutcome.ExternalUserNotAllowed;
        }

        BootstrapSiteAdminOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.SiteAdmin, ct);
                await roles.LockRoleAsync(roleId, ct);

                if (await roles.IsHeldByAsync(userId, roleId, ct))
                {
                    return BootstrapSiteAdminOutcome.AlreadyGranted;
                }

                // Disabled holders don't count: they can't administer anything.
                if (await roles.CountActiveHoldersAsync(roleId, excludingUserId: userId, ct) > 0)
                {
                    return BootstrapSiteAdminOutcome.ActiveSiteAdminExists;
                }

                roles.AddUserRole(UserRole.Assign(userId, roleId, UserType.Internal, assignedByUserId: null, timeProvider.GetUtcNow()));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.SiteAdminBootstrapped,
                    UserId: null,
                    Metadata: JsonSerializer.Serialize(new { grantedUserId = userId, identityPlatformUid = command.IdentityPlatformUid }),
                    ActorType: AuditActorType.System));

                await unitOfWork.SaveChangesAsync(ct);

                return BootstrapSiteAdminOutcome.Granted;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.RoleAssignment)
        {
            return BootstrapSiteAdminOutcome.AlreadyGranted;
        }

        if (outcome == BootstrapSiteAdminOutcome.Granted)
        {
            accessCache.EvictUser(userId);
        }

        return outcome;
    }
}
