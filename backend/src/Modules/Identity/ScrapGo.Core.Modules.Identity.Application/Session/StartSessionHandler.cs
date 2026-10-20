using System.Text.Json;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Invitations;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Session;

/// <summary>Everything about the caller comes from the validated token, never from the request body.</summary>
/// <param name="IdentityPlatformUid">The token's <c>sub</c>.</param>
/// <param name="Email">The token's <c>email</c>; a display attribute.</param>
/// <param name="EmailVerified">The token's <c>email_verified</c>.</param>
/// <param name="SignInProvider">The token's <c>firebase.sign_in_provider</c>, e.g. <c>google.com</c>.</param>
/// <param name="HostedDomain">The token's <c>hd</c>, present only for Google Workspace sign-ins.</param>
/// <param name="DisplayName">The token's <c>name</c>, when sent.</param>
/// <param name="InvitationToken">The one-time token from an invitation's accept link: the only body field.</param>
public sealed record StartSessionCommand(
    string IdentityPlatformUid,
    string Email,
    bool EmailVerified,
    string? SignInProvider,
    string? HostedDomain,
    string? DisplayName,
    string? InvitationToken = null);

public enum StartSessionOutcome
{
    Started,

    /// <summary>The user exists but isn't Active.</summary>
    AccountDisabled,

    /// <summary>No user, and neither the internal rule nor a valid invitation applies. Nothing was created.</summary>
    NoAccess,
}

public sealed record StartSessionResult(StartSessionOutcome Outcome, MeResponse? Me = null);

/// <summary>
/// <c>POST /api/session</c>, the end of every sign-in (ACCESS-MODEL.md
/// section 7) and the only place users are created.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A known, Active user: their sign-in is recorded and their access context returned.</item>
/// <item>A known user who isn't Active: <see cref="StartSessionOutcome.AccountDisabled"/>.</item>
/// <item>No user, and the internal rule holds (provider <c>google.com</c>,
/// <c>email_verified</c>, <c>hd</c> on <c>INTERNAL_HD_ALLOWLIST</c>): an
/// Internal user is created with <c>scrapgo_user</c>, exactly once
/// (<see cref="IUserRepository.InsertIfAbsentAsync"/>).</item>
/// <item>No user, and a valid invitation token: an External user is created
/// with the invited role and supplier links, and the invitation is accepted,
/// in one transaction. Valid means Pending, unexpired, <c>email_verified</c>,
/// and the token's email matching the invitation's (case-insensitive).</item>
/// <item>Anything else: <see cref="StartSessionOutcome.NoAccess"/>; no row is created.</item>
/// </list>
/// </remarks>
public sealed class StartSessionHandler(
    IUserRepository users,
    IRoleRepository roles,
    IInvitationRepository invitations,
    IAccessContextProvider accessContexts,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<InternalHdAllowlistOptions> hdAllowlist,
    TimeProvider timeProvider)
{
    public const string GoogleProvider = "google.com";

    /// <summary>The 403 <c>reason</c> for a sign-in with no user and no route to one.</summary>
    public const string NoAccessReason = "no_access";

    public async Task<StartSessionResult> HandleAsync(StartSessionCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetIdByUidAsync(command.IdentityPlatformUid, cancellationToken) is { } existingId)
        {
            return await ResumeAsync(existingId, command, cancellationToken);
        }

        if (PassesInternalRule(command))
        {
            var internalUserId = await ProvisionInternalAsync(command, cancellationToken);
            return await ResumeAsync(internalUserId, command, cancellationToken);
        }

        if (command.InvitationToken is { Length: > 0 } token)
        {
            var (invitedUserId, problem) = await AcceptInvitationAsync(command, token, cancellationToken);
            if (invitedUserId is { } userId)
            {
                return await ResumeAsync(userId, command, cancellationToken);
            }

            await DenyAsync(userId: null, NoAccessReason, command, cancellationToken, problem);
            return new(StartSessionOutcome.NoAccess);
        }

        await DenyAsync(userId: null, NoAccessReason, command, cancellationToken);
        return new(StartSessionOutcome.NoAccess);
    }

    /// <summary>
    /// Accepts the invitation and creates its External user, role and supplier
    /// links in one transaction. The invitation row is locked first, so one
    /// token is accepted at most once however many requests race.
    /// </summary>
    /// <returns>The new user's id, or why the invitation was refused (audited, never shown to the caller).</returns>
    private Task<(int? UserId, string? Problem)> AcceptInvitationAsync(
        StartSessionCommand command, string token, CancellationToken cancellationToken)
    {
        var tokenHash = InvitationTokens.Hash(token);
        var now = timeProvider.GetUtcNow();

        return unitOfWork.ExecuteInTransactionAsync<(int? UserId, string? Problem)>(async ct =>
        {
            await invitations.LockByTokenHashAsync(tokenHash, ct);
            var invitation = await invitations.FindByTokenHashAsync(tokenHash, ct);

            var problem = invitation switch
            {
                null => "invitation_not_found",
                { Status: InvitationStatus.Accepted } => "invitation_used",
                { Status: InvitationStatus.Revoked } => "invitation_revoked",
                _ when !invitation.IsOpenAt(now) => "invitation_expired",
                _ when !command.EmailVerified => "email_unverified",
                _ when Invitation.NormalizeEmail(command.Email) != invitation.Email => "email_mismatch",
                _ => null,
            };
            if (problem is not null)
            {
                return (null, problem);
            }

            if (await roles.FindActiveRoleAsync(invitation!.RoleId, ct) is not { UserType: UserType.External } role)
            {
                return (null, "invitation_role_invalid");
            }

            var candidate = User.Provision(
                command.IdentityPlatformUid, command.Email, UserType.External, now, command.DisplayName, command.SignInProvider);
            var provisioned = await users.InsertIfAbsentAsync(candidate, ct);
            if (!provisioned.WasCreated)
            {
                // The same UID won a race through another path; it signs in as that user.
                return (provisioned.User.Id, null);
            }

            var userId = provisioned.User.Id;
            roles.AddUserRole(UserRole.Assign(userId, role.Id, UserType.External, assignedByUserId: null, now));
            users.AddSupplierLinks(invitation.SupplierRecordIds.Select(id => UserSupplier.Link(userId, id, linkedByUserId: null, now)));
            invitation.Accept(userId, now);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.InvitationAccepted,
                UserId: userId,
                Metadata: JsonSerializer.Serialize(new
                {
                    invitationId = invitation.Id,
                    roleId = role.Id,
                    supplierRecordIds = invitation.SupplierRecordIds,
                })));
            await unitOfWork.SaveChangesAsync(ct);

            return (userId, null);
        }, cancellationToken);
    }

    private bool PassesInternalRule(StartSessionCommand command) =>
        command.SignInProvider == GoogleProvider
        && command.EmailVerified
        && hdAllowlist.Value.Contains(command.HostedDomain);

    /// <summary>
    /// Creates the Internal user and their <c>scrapgo_user</c> role in one
    /// transaction. A concurrent request for the same UID loses the insert
    /// race and just reads the winner's row (and role).
    /// </summary>
    private Task<int> ProvisionInternalAsync(StartSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidate = User.Provision(
            command.IdentityPlatformUid, command.Email, UserType.Internal, now, command.DisplayName, command.SignInProvider);

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var provisioned = await users.InsertIfAbsentAsync(candidate, ct);
            if (provisioned.WasCreated)
            {
                var roleId = await roles.GetPlatformRoleIdAsync(DefaultRoleNames.ScrapGoUser, ct);
                roles.AddUserRole(UserRole.Assign(provisioned.User.Id, roleId, UserType.Internal, assignedByUserId: null, now));
                auditLog.Record(new AuditEvent(IdentityAuditEventTypes.UserProvisioned, UserId: provisioned.User.Id));
                await unitOfWork.SaveChangesAsync(ct);
            }

            return provisioned.User.Id;
        }, cancellationToken);
    }

    private async Task<StartSessionResult> ResumeAsync(int userId, StartSessionCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is not { } user)
        {
            return new(StartSessionOutcome.NoAccess);
        }

        if (!user.IsActive)
        {
            await DenyAsync(user.Id, EvaluateUserStatusGateHandler.AccountDisabledReason, command, cancellationToken);
            return new(StartSessionOutcome.AccountDisabled);
        }

        user.RecordSignIn(command.SignInProvider, timeProvider.GetUtcNow());
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.SessionStarted,
            UserId: user.Id,
            Metadata: JsonSerializer.Serialize(new { provider = command.SignInProvider })));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // A new session is a natural point to drop anything cached about the user.
        accessCache.EvictUser(user.Id);
        var access = await accessContexts.LoadAsync(user.IdentityPlatformUid, cancellationToken);

        return access is null ? new(StartSessionOutcome.NoAccess) : new(StartSessionOutcome.Started, MeResponse.From(user, access));
    }

    private async Task DenyAsync(
        int? userId, string reason, StartSessionCommand command, CancellationToken cancellationToken, string? invitationProblem = null)
    {
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.AccessDenied,
            UserId: userId,
            Metadata: JsonSerializer.Serialize(new
            {
                reason,
                path = "/api/session",
                uid = command.IdentityPlatformUid,
                provider = command.SignInProvider,
                emailVerified = command.EmailVerified,
                hd = command.HostedDomain,
                invitation = invitationProblem,
            })));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
