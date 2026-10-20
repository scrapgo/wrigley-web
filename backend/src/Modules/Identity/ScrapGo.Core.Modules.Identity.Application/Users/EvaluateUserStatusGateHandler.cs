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
/// <remarks>
/// It reads the caller's access context (<see cref="ICurrentAccess"/>), the
/// same one authorization uses; changing a user's status evicts it from the cache.
/// </remarks>
public sealed class EvaluateUserStatusGateHandler(
    ICurrentAccess currentAccess,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<ExternalUserMfaOptions> mfaOptions)
{
    /// <param name="hasSecondFactor">Whether the bearer token records a second factor (<c>firebase.sign_in_second_factor</c>).</param>
    public async Task<UserStatusGateDecision> HandleAsync(bool hasSecondFactor, CancellationToken cancellationToken)
    {
        var access = await currentAccess.GetAsync(cancellationToken);

        // A UID with no row yet has nothing to disable. Its first request must
        // still reach whatever endpoint provisions it. The check is "!= Active"
        // rather than "== Disabled" so any future status fails closed.
        if (access is not null && access.Status != UserStatus.Active)
        {
            auditLog.Record(new AuditEvent(IdentityAuditEventTypes.DeniedDisabledUser, UserId: access.UserId));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return UserStatusGateDecision.Deny;
        }

        if (mfaOptions.Value.RequireForExternalUsers
            && access is { UserType: UserType.External }
            && !hasSecondFactor)
        {
            return UserStatusGateDecision.DenyMfaRequired;
        }

        return UserStatusGateDecision.Allow;
    }
}
