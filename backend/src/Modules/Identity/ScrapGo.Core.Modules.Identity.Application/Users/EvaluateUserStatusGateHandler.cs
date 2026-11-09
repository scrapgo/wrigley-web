using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Mfa;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

public enum UserStatusGateDecision
{
    /// <summary>No user row exists yet, or its status is Active (and any MFA requirement is met).</summary>
    Allow,

    /// <summary>A user row exists and its status is not Active.</summary>
    Deny,

    /// <summary>
    /// An external user whose sign-in has neither a GCIP second factor nor a
    /// verified email code, while <see cref="ExternalUserMfaOptions.RequireForExternalUsers"/> is on.
    /// </summary>
    DenyMfaRequired,
}

/// <summary>
/// Disabled-user gate, evaluated once per authenticated request before any
/// endpoint runs, so disabling a user takes effect on the very next request
/// rather than when their token expires. It also enforces the MFA
/// requirement for external users (off by default): a GCIP second factor, or
/// an email code verified for this sign-in (<see cref="MfaService"/>).
/// </summary>
/// <remarks>
/// It reads the caller's access context (<see cref="ICurrentAccess"/>), the
/// same one authorization uses; changing a user's status evicts it from the cache.
/// </remarks>
public sealed class EvaluateUserStatusGateHandler(
    ICurrentAccess currentAccess,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    MfaService mfa)
{
    /// <summary>The denial reason, in the 403 body and the <c>access.denied</c> audit row.</summary>
    public const string AccountDisabledReason = "account_disabled";

    /// <param name="hasSecondFactor">Whether the bearer token records a second factor (<c>firebase.sign_in_second_factor</c>).</param>
    /// <param name="authTime">The token's <c>auth_time</c>: the sign-in an email code is verified for.</param>
    /// <param name="checkMfa">False on the routes that send and verify the email code, which must be reachable before it's verified.</param>
    public async Task<UserStatusGateDecision> HandleAsync(
        bool hasSecondFactor, DateTimeOffset? authTime, bool checkMfa, CancellationToken cancellationToken)
    {
        var access = await currentAccess.GetAsync(cancellationToken);

        // A UID with no row yet has nothing to disable. Its first request must
        // still reach whatever endpoint provisions it. The check is "!= Active"
        // rather than "== Disabled" so any future status fails closed.
        if (access is not null && access.Status != UserStatus.Active)
        {
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.AccessDenied,
                UserId: access.UserId,
                Metadata: JsonSerializer.Serialize(new { reason = AccountDisabledReason })));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return UserStatusGateDecision.Deny;
        }

        if (checkMfa
            && access is not null
            && !hasSecondFactor
            && await mfa.IsRequiredAsync(access, authTime, cancellationToken))
        {
            return UserStatusGateDecision.DenyMfaRequired;
        }

        return UserStatusGateDecision.Allow;
    }
}
