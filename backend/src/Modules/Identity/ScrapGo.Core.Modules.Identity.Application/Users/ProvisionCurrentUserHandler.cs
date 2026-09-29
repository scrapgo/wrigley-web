using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <param name="IdentityPlatformUid">From the validated token's <c>sub</c> claim, never from request body or query.</param>
/// <param name="Email">From the <c>email</c> claim. A display attribute only; its absence doesn't block provisioning.</param>
/// <param name="HostedDomain">From the <c>hd</c> claim. Null for personal Google accounts.</param>
public sealed record ProvisionCurrentUserCommand(string IdentityPlatformUid, string Email, string? HostedDomain);

/// <summary>
/// First-request auto-provisioning: resolves the caller's user row, creating
/// it on first sight. Exactly-once under concurrency, guaranteed by
/// <see cref="IUserRepository.InsertIfAbsentAsync"/>.
/// </summary>
public sealed class ProvisionCurrentUserHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<InternalHdAllowlistOptions> hdAllowlist,
    TimeProvider timeProvider)
{
    public Task<CurrentUserDto> HandleAsync(ProvisionCurrentUserCommand command, CancellationToken cancellationToken)
    {
        // Only takes effect on the insert branch. If the UID already has a
        // row, that row's classification wins, so a later change to the hd
        // claim never reclassifies an existing user.
        var classification = hdAllowlist.Value.Contains(command.HostedDomain)
            ? UserClassification.Internal
            : UserClassification.External;

        var candidate = User.Provision(command.IdentityPlatformUid, command.Email, classification, timeProvider.GetUtcNow());

        // In a transaction so the audit row commits or rolls back with the insert.
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var provisioned = await users.InsertIfAbsentAsync(candidate, ct);

            // Audit only the genuine first sighting. The conflict branch is
            // just a read of a row another request already provisioned and
            // audited.
            if (provisioned.WasCreated)
            {
                auditLog.Record(new AuditEvent(IdentityAuditEventTypes.UserProvisioned, UserId: provisioned.User.Id));
                await unitOfWork.SaveChangesAsync(ct);
            }

            return CurrentUserDto.From(provisioned.User);
        }, cancellationToken);
    }
}
