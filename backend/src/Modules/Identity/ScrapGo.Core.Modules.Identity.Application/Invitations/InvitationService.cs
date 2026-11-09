using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Modules.Identity.Domain.Authorization;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <summary>
/// Invitation administration (ACCESS-MODEL.md sections 7 and 9). Acceptance
/// is part of <c>POST /api/session</c> (<see cref="Session.StartSessionHandler"/>).
/// </summary>
/// <remarks>
/// The routes are authorized before these methods run: listing needs
/// <c>users.view</c> or <c>users.invite</c>, everything else <c>users.invite</c>.
/// Granting Site Admin also needs <c>users.manage</c>, checked here, so a
/// ScrapGo User can't create an admin. Each change commits with its audit row.
/// </remarks>
public sealed class InvitationService(
    IInvitationRepository invitations,
    IRoleRepository roles,
    IUserRepository users,
    ICurrentAccess currentAccess,
    IInvitationSender sender,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    IOptions<InvitationOptions> options,
    IOptions<InternalHdAllowlistOptions> internalDomains,
    TimeProvider timeProvider,
    ILogger<InvitationService> logger)
{
    /// <summary>
    /// The internal email domains and every role an invitation can grant, for
    /// the invite form. Needs only <c>users.invite</c>, not <c>roles.view</c>.
    /// </summary>
    public async Task<InvitationFormDto> GetFormAsync(CancellationToken cancellationToken) =>
        new(
            [.. internalDomains.Value.HostedDomains.Select(domain => domain.ToLowerInvariant()).Order(StringComparer.Ordinal)],
            [
                .. (await roles.ListRolesAsync(cancellationToken))
                    .Select(role => new InvitableRoleDto(
                        role.Id, role.Key, role.Name, role.Description, role.UserType, RequiresUsersManage(role.Key))),
            ]);

    public async Task<PagedResult<InvitationDto>?> ListAsync(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        if (!page.IsValid || !TryParseStatus(query.Status, out var status))
        {
            return null;
        }

        return await invitations.ListAsync(status, page, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <returns>Null when there is no such invitation. The token is never readable again after create or resend.</returns>
    public async Task<InvitationDto?> GetAsync(int invitationId, CancellationToken cancellationToken) =>
        await invitations.FindAsync(invitationId, cancellationToken) is { } invitation
            ? InvitationDto.From(invitation, await RoleKeysOfAsync(invitation.RoleIds, cancellationToken), timeProvider.GetUtcNow())
            : null;

    public async Task<InvitationResult> CreateAsync(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        if (!IsWellFormedEmail(command.Email)
            || command.RoleIds is not { Count: > 0 } requestedRoleIds
            || requestedRoleIds.Any(id => id <= 0)
            || command.SupplierRecordIds?.Any(id => id <= 0) == true)
        {
            return new(InvitationOutcome.InvalidRequest);
        }

        // Someone who already has an account needs role changes, not an invitation.
        if (await users.ExistsByEmailAsync(command.Email!, cancellationToken))
        {
            return new(InvitationOutcome.UserExists);
        }

        // The email's domain decides the user type: staff addresses join as
        // Internal (at their first Google sign-in), everyone else as External.
        var userType = internalDomains.Value.Contains(DomainOf(command.Email!)) ? UserType.Internal : UserType.External;
        var supplierRecordIds = command.SupplierRecordIds ?? [];

        // External users see only their linked suppliers; internal users see them all.
        if (userType == UserType.External ? supplierRecordIds.Count == 0 : supplierRecordIds.Count > 0)
        {
            return new(InvitationOutcome.InvalidRequest);
        }

        int[] roleIds = [.. requestedRoleIds.Distinct()];
        var granted = await roles.FindRolesAsync(roleIds, cancellationToken);
        if (granted.Count != roleIds.Length)
        {
            return new(InvitationOutcome.RoleNotFound);
        }

        if (granted.Any(role => role.UserType != userType))
        {
            return new(InvitationOutcome.RoleTypeMismatch);
        }

        // The route's permission check has already loaded the caller's access context.
        var access = await currentAccess.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("An authorized invitation request has no access context.");
        if (granted.Any(role => RequiresUsersManage(role.Key)) && !access.Can(Permissions.UsersManage))
        {
            return new(InvitationOutcome.SiteAdminRequiresUsersManage);
        }

        var actorId = access.UserId;
        var now = timeProvider.GetUtcNow();
        var (token, hash) = InvitationTokens.Generate();
        var invitation = Invitation.Create(
            command.Email!, userType, roleIds, supplierRecordIds, hash, now + options.Value.Lifetime, actorId, now);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await invitations.ExpireLapsedAsync(invitation.Email, now, ct);
                invitations.Add(invitation);
                await unitOfWork.SaveChangesAsync(ct);

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.InvitationCreated,
                    UserId: actorId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        invitationId = invitation.Id,
                        email = invitation.Email,
                        userType = invitation.UserType.ToString(),
                        roleIds = invitation.RoleIds,
                        supplierRecordIds = invitation.SupplierRecordIds,
                    })));
                await unitOfWork.SaveChangesAsync(ct);

                return true;
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException e) when (e.ConstraintName == IdentityUniqueConstraints.PendingInvitationEmail)
        {
            return new(InvitationOutcome.AlreadyPending);
        }

        return new(
            InvitationOutcome.Success,
            await IssueAsync(invitation, granted.Select(role => role.Key), token, now, command.RequestOrigin, cancellationToken));
    }

    /// <summary>
    /// Issues a new token with a fresh expiry (<see cref="InvitationOptions.Lifetime"/>);
    /// the old link stops working. A Pending or Expired invitation can be resent.
    /// </summary>
    /// <param name="requestOrigin">The portal the admin is using; the new link points back to it when listed.</param>
    public async Task<InvitationResult> ResendAsync(int invitationId, string? requestOrigin, CancellationToken cancellationToken)
    {
        if (await invitations.FindAsync(invitationId, cancellationToken) is not { } invitation)
        {
            return new(InvitationOutcome.NotFound);
        }

        if (invitation.Status is not (InvitationStatus.Pending or InvitationStatus.Expired))
        {
            return new(InvitationOutcome.NotPending);
        }

        // They may have joined some other way since; their roles are changed on the user list.
        if (await users.ExistsByEmailAsync(invitation.Email, cancellationToken))
        {
            return new(InvitationOutcome.UserExists);
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var (token, hash) = InvitationTokens.Generate();
        invitation.Reissue(hash, now + options.Value.Lifetime);
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.InvitationResent,
            UserId: actorId,
            Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, email = invitation.Email })));
        if (!await TrySaveReopenedAsync(cancellationToken))
        {
            return new(InvitationOutcome.AlreadyPending);
        }

        var roleKeys = await RoleKeysOfAsync(invitation.RoleIds, cancellationToken);
        return new(InvitationOutcome.Success, await IssueAsync(invitation, roleKeys, token, now, requestOrigin, cancellationToken));
    }

    /// <summary>
    /// What the accept page shows before anyone signs up: the invitation's
    /// effective status and a masked email. Anonymous, so it reveals nothing else.
    /// </summary>
    public async Task<InvitationLookupDto> LookupAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)
            || await invitations.FindByTokenHashAsync(InvitationTokens.Hash(token), cancellationToken) is not { } invitation)
        {
            return new(InvitationLookupDto.NotFound, null, null);
        }

        var status = InvitationDto.EffectiveStatus(invitation.Status, invitation.ExpiresAt, timeProvider.GetUtcNow());
        return new(status.ToString(), MaskEmail(invitation.Email), invitation.ExpiresAt);
    }

    /// <summary>
    /// The invitee asks for a new link with their expired one. The new link is
    /// emailed to the invited address only, never returned. Anonymous: the
    /// token is the credential. Limited to one per
    /// <see cref="InvitationOptions.RenewCooldown"/> and
    /// <see cref="InvitationOptions.MaxRenewals"/> in all, until an admin resends.
    /// </summary>
    public async Task<InvitationOutcome> RenewAsync(string? token, string? requestOrigin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)
            || await invitations.FindByTokenHashAsync(InvitationTokens.Hash(token), cancellationToken) is not { } invitation)
        {
            return InvitationOutcome.NotFound;
        }

        var now = timeProvider.GetUtcNow();

        // Only an expired external invitation: a live one still works, an accepted
        // or revoked one is over, and internal invitations have no token to renew.
        if (invitation.UserType != UserType.External
            || InvitationDto.EffectiveStatus(invitation.Status, invitation.ExpiresAt, now) != InvitationStatus.Expired
            || await users.ExistsByEmailAsync(invitation.Email, cancellationToken))
        {
            return InvitationOutcome.NotRenewable;
        }

        if (invitation.RenewCount >= InvitationOptions.MaxRenewals)
        {
            return InvitationOutcome.RenewLimitReached;
        }

        if (invitation.LastRenewedAt is { } last && now - last < InvitationOptions.RenewCooldown)
        {
            return InvitationOutcome.RenewTooSoon;
        }

        var (newToken, hash) = InvitationTokens.Generate();
        invitation.Renew(hash, now + options.Value.Lifetime, now);
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.InvitationRenewed,
            ActorType: AuditActorType.System,
            Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, email = invitation.Email, renewCount = invitation.RenewCount })));
        if (!await TrySaveReopenedAsync(cancellationToken))
        {
            return InvitationOutcome.NotRenewable;
        }

        await IssueAsync(invitation, [], newToken, now, requestOrigin, cancellationToken);
        return InvitationOutcome.Success;
    }

    /// <summary>
    /// Saves an invitation made Pending again. False when the email meanwhile
    /// got another pending invitation (one pending per email).
    /// </summary>
    private async Task<bool> TrySaveReopenedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (UniqueConstraintViolationException e) when (e.ConstraintName == IdentityUniqueConstraints.PendingInvitationEmail)
        {
            return false;
        }
    }

    /// <summary><c>jane@acme.com</c> as <c>j***@acme.com</c>: enough for the invitee to recognise, too little to harvest.</summary>
    private static string MaskEmail(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    /// <summary>Revoking is idempotent; an accepted invitation can't be revoked.</summary>
    public async Task<InvitationOutcome> RevokeAsync(int invitationId, CancellationToken cancellationToken)
    {
        if (await invitations.FindAsync(invitationId, cancellationToken) is not { } invitation)
        {
            return InvitationOutcome.NotFound;
        }

        switch (invitation.Status)
        {
            case InvitationStatus.Revoked:
                return InvitationOutcome.Success;
            case InvitationStatus.Accepted:
                return InvitationOutcome.NotPending;
        }

        var actorId = await ActorIdAsync(cancellationToken);
        invitation.Revoke();
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.InvitationRevoked,
            UserId: actorId,
            Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, email = invitation.Email })));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return InvitationOutcome.Success;
    }

    private async Task<IssuedInvitationDto> IssueAsync(
        Invitation invitation,
        IEnumerable<string> roleKeys,
        string token,
        DateTimeOffset now,
        string? requestOrigin,
        CancellationToken cancellationToken)
    {
        // Staff need no token: their first Google sign-in finds the invitation by email.
        var signInWithGoogle = invitation.UserType == UserType.Internal;
        var acceptLink = signInWithGoogle
            ? options.Value.BuildSignInLink(requestOrigin)
            : options.Value.BuildAcceptLink(token, requestOrigin);

        // The invitation is already committed: a failed email mustn't undo it.
        // The admin still gets the link to share by hand.
        bool emailSent;
        try
        {
            emailSent = await sender.SendAsync(
                new InvitationMessage(invitation.Id, invitation.Email, acceptLink, invitation.ExpiresAt, signInWithGoogle), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            emailSent = false;
            logger.LogWarning(ex, "Invitation {InvitationId} was created but its email could not be sent.", invitation.Id);
        }

        return new IssuedInvitationDto(InvitationDto.From(invitation, roleKeys, now), acceptLink, emailSent);
    }

    /// <summary>The route's permission check has already loaded the caller's access context.</summary>
    private async Task<int> ActorIdAsync(CancellationToken cancellationToken) =>
        (await currentAccess.GetAsync(cancellationToken))?.UserId
        ?? throw new InvalidOperationException("An authorized invitation request has no access context.");

    private async Task<IEnumerable<string>> RoleKeysOfAsync(IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken) =>
        (await roles.FindRolesAsync(roleIds, cancellationToken)).Select(role => role.Key);

    /// <summary>Site Admin can change anything, so only a holder of <c>users.manage</c> may invite one.</summary>
    private static bool RequiresUsersManage(string roleKey) => roleKey == DefaultRoleNames.SiteAdmin;

    private static string DomainOf(string email) => email.Trim()[(email.Trim().LastIndexOf('@') + 1)..];

    private static bool IsWellFormedEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && MailAddress.TryCreate(email.Trim(), out var address)
        && string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Names only; numeric strings are rejected rather than cast to the enum.</summary>
    private static bool TryParseStatus(string? value, out InvitationStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsDigit) || !Enum.TryParse<InvitationStatus>(value, ignoreCase: true, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }
}
