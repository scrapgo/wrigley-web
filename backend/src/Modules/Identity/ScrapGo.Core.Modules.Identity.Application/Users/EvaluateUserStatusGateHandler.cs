using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

public enum UserStatusGateDecision
{
    /// <summary>No user row exists yet, or its status is Active (and any MFA requirement is met).</summary>
    Allow,

    /// <summary>A user row exists and its status is not Active.</summary>
    Deny,

    /// <summary>
    /// An external user without a second factor, while
    /// <see cref="ExternalUserMfaOptions.RequireForExternalUsers"/> is on.
    /// </summary>
    DenyMfaRequired,
}

/// <summary>
/// Disabled-user gate, evaluated once per authenticated request before any
/// endpoint runs, so disabling a user takes effect on the very next request
/// rather than when their token expires. It also enforces the reserved MFA
/// requirement for external users (off by default).
/// </summary>
public sealed class EvaluateUserStatusGateHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<ExternalUserMfaOptions> mfaOptions)
{
    /// <param name="hasSecondFactor">Whether the bearer token records a second factor (<c>firebase.sign_in_second_factor</c>).</param>
    public async Task<UserStatusGateDecision> HandleAsync(
        string identityPlatformUid, bool hasSecondFactor, CancellationToken cancellationToken)
    {
        var snapshot = await users.GetStatusByUidAsync(identityPlatformUid, cancellationToken);

        // A UID with no row yet has nothing to disable. Its first request must
        // still reach whatever endpoint provisions it. The check is "!= Active"
        // rather than "== Disabled" so any future status fails closed.
        if (snapshot is not null && snapshot.Status != UserStatus.Active)
        {
            auditLog.Record(new AuditEvent(IdentityAuditEventTypes.DeniedDisabledUser, UserId: snapshot.UserId));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return UserStatusGateDecision.Deny;
        }

        if (mfaOptions.Value.RequireForExternalUsers
            && snapshot is { Classification: UserClassification.External }
            && !hasSecondFactor)
        {
            return UserStatusGateDecision.DenyMfaRequired;
        }

        return UserStatusGateDecision.Allow;
    }
}
