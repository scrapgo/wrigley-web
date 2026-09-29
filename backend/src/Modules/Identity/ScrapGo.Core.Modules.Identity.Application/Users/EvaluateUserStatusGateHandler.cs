namespace ScrapGo.Core.Modules.Identity.Application.Users;

public enum UserStatusGateDecision
{
    /// <summary>No user row exists yet, or its status is Active.</summary>
    Allow,

    /// <summary>A user row exists and its status is not Active.</summary>
    Deny,
}

/// <summary>
/// Disabled-user gate, evaluated once per authenticated request before any
/// endpoint runs, so disabling a user takes effect on the very next request
/// rather than when their token expires.
/// </summary>
public sealed class EvaluateUserStatusGateHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog)
{
    public async Task<UserStatusGateDecision> HandleAsync(string identityPlatformUid, CancellationToken cancellationToken)
    {
        var snapshot = await users.GetStatusByUidAsync(identityPlatformUid, cancellationToken);

        // A UID with no row yet has nothing to disable. Its first request must
        // still reach whatever endpoint provisions it. The check is "!= Active"
        // rather than "== Disabled" so any future status fails closed.
        if (snapshot is null || snapshot.Status == UserStatus.Active)
        {
            return UserStatusGateDecision.Allow;
        }

        auditLog.Record(new AuditEvent(IdentityAuditEventTypes.DeniedDisabledUser, UserId: snapshot.UserId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserStatusGateDecision.Deny;
    }
}
