using System.Text.Json;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Session;

/// <summary>Everything about the caller comes from the validated token, never from the request body.</summary>
/// <param name="IdentityPlatformUid">The token's <c>sub</c>.</param>
/// <param name="Email">The token's <c>email</c>; a display attribute.</param>
/// <param name="EmailVerified">The token's <c>email_verified</c>.</param>
/// <param name="SignInProvider">The token's <c>firebase.sign_in_provider</c>, e.g. <c>google.com</c>.</param>
/// <param name="HostedDomain">The token's <c>hd</c>, present only for Google Workspace sign-ins.</param>
/// <param name="DisplayName">The token's <c>name</c>, when sent.</param>
public sealed record StartSessionCommand(
    string IdentityPlatformUid,
    string Email,
    bool EmailVerified,
    string? SignInProvider,
    string? HostedDomain,
    string? DisplayName);

public enum StartSessionOutcome
{
    Started,

    /// <summary>The user exists but isn't Active.</summary>
    AccountDisabled,

    /// <summary>No user, and the sign-in doesn't pass the internal rule. Nothing was created.</summary>
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
/// <item>Anything else: <see cref="StartSessionOutcome.NoAccess"/>; no row is created.</item>
/// </list>
/// Invitation acceptance (the External path) arrives in B11.
/// </remarks>
public sealed class StartSessionHandler(
    IUserRepository users,
    IRoleRepository roles,
    IAccessContextProvider accessContexts,
    IAccessCache accessCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<InternalHdAllowlistOptions> hdAllowlist,
    TimeProvider timeProvider)
{
    public const string GoogleProvider = "google.com";

    public async Task<StartSessionResult> HandleAsync(StartSessionCommand command, CancellationToken cancellationToken)
    {
        if (await users.GetIdByUidAsync(command.IdentityPlatformUid, cancellationToken) is { } existingId)
        {
            return await ResumeAsync(existingId, command, cancellationToken);
        }

        if (!PassesInternalRule(command))
        {
            await DenyAsync(userId: null, "no_access", command, cancellationToken);
            return new(StartSessionOutcome.NoAccess);
        }

        var userId = await ProvisionInternalAsync(command, cancellationToken);
        return await ResumeAsync(userId, command, cancellationToken);
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

    private async Task DenyAsync(int? userId, string reason, StartSessionCommand command, CancellationToken cancellationToken)
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
            })));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
