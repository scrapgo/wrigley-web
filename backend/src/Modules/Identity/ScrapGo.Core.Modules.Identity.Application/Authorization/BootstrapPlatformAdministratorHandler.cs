using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.Application.Authorization;

/// <param name="IdentityPlatformUid">The GCIP UID of an already-provisioned user.</param>
public sealed record BootstrapPlatformAdministratorCommand(string IdentityPlatformUid);

public enum BootstrapPlatformAdministratorOutcome
{
    /// <summary>The user now holds PlatformAdministrator at platform scope.</summary>
    Granted,

    /// <summary>The user already held it; nothing was written.</summary>
    AlreadyGranted,

    /// <summary>No user row for the UID. They must sign in and call <c>GET /api/users/me</c> once first.</summary>
    UserNotProvisioned,

    /// <summary>A different user already holds it. Bootstrap is one-time; later grants go through the role-assignment API.</summary>
    AnotherAdministratorExists,

    /// <summary>The user is External (a customer). Platform roles are for internal (Google Workspace) users only.</summary>
    ExternalUserNotAllowed,
}

/// <summary>
/// Grants the first <see cref="DefaultRoleNames.PlatformAdministrator"/>,
/// the only way a platform-scoped role assignment is created outside the
/// role-assignment API. It runs from the host's <c>bootstrap-platform-admin</c>
/// command only, never over HTTP and never at sign-in.
/// </summary>
/// <remarks>
/// It is idempotent for the same UID and refuses once any other user holds the
/// role. The role row is locked for the duration of the check-then-assign, so
/// concurrent runs serialize. The grant is audited as a system action.
/// </remarks>
public sealed class BootstrapPlatformAdministratorHandler(
    IUserRepository users,
    IRoleRepository roles,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    public async Task<BootstrapPlatformAdministratorOutcome> HandleAsync(
        BootstrapPlatformAdministratorCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdentityPlatformUid)
            || await users.GetIdByUidAsync(command.IdentityPlatformUid, cancellationToken) is not { } userId)
        {
            return BootstrapPlatformAdministratorOutcome.UserNotProvisioned;
        }

        if (await users.GetByIdAsync(userId, cancellationToken) is { Classification: UserClassification.External })
        {
            return BootstrapPlatformAdministratorOutcome.ExternalUserNotAllowed;
        }

        BootstrapPlatformAdministratorOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.PlatformAdministrator, ct);
                await roles.LockRoleAsync(roleId, ct);

                var platformHolders = (await roles.GetAssignmentScopesAsync(roleId, ct))
                    .Where(s => s.IsPlatform)
                    .Select(s => s.UserId)
                    .ToList();

                if (platformHolders.Contains(userId))
                {
                    return BootstrapPlatformAdministratorOutcome.AlreadyGranted;
                }

                if (platformHolders.Count > 0)
                {
                    return BootstrapPlatformAdministratorOutcome.AnotherAdministratorExists;
                }

                roles.AddUserRole(UserRole.Assign(userId, roleId, organizationId: null, timeProvider.GetUtcNow()));
                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.PlatformAdministratorBootstrapped,
                    UserId: null,
                    OrganizationId: null,
                    Metadata: JsonSerializer.Serialize(new { grantedUserId = userId, identityPlatformUid = command.IdentityPlatformUid }),
                    ActorType: AuditActorType.System));

                await unitOfWork.SaveChangesAsync(ct);

                return BootstrapPlatformAdministratorOutcome.Granted;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.PlatformRoleAssignment)
        {
            return BootstrapPlatformAdministratorOutcome.AlreadyGranted;
        }

        if (outcome == BootstrapPlatformAdministratorOutcome.Granted)
        {
            await permissionCache.InvalidateAsync(new PermissionScope(userId, null), cancellationToken);
        }

        return outcome;
    }
}
