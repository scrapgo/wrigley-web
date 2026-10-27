using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <summary>
/// Invitation administration (ACCESS-MODEL.md sections 7 and 9). Acceptance
/// is part of <c>POST /api/session</c> (<see cref="Session.StartSessionHandler"/>).
/// </summary>
/// <remarks>
/// The routes are authorized before these methods run: listing needs
/// <c>users.view</c> or <c>users.invite</c>, everything else <c>users.invite</c>.
/// Each change commits with its audit row.
/// </remarks>
public sealed class InvitationService(
    IInvitationRepository invitations,
    IRoleRepository roles,
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
    /// The roles an invitation can grant (external roles only), for the invite
    /// form. Needs only <c>users.invite</c>, not <c>roles.view</c>.
    /// </summary>
    public async Task<IReadOnlyList<InvitableRoleDto>> ListInvitableRolesAsync(CancellationToken cancellationToken) =>
        [
            .. (await roles.ListRolesAsync(cancellationToken))
                .Where(role => role.UserType == nameof(UserType.External))
                .Select(role => new InvitableRoleDto(role.Id, role.Key, role.Name, role.Description)),
        ];

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
            ? InvitationDto.From(invitation, await RoleKeyOfAsync(invitation.RoleId, cancellationToken), timeProvider.GetUtcNow())
            : null;

    public async Task<InvitationResult> CreateAsync(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        if (!IsWellFormedEmail(command.Email)
            || command.RoleId <= 0
            || command.SupplierRecordIds is not { Count: > 0 } supplierRecordIds
            || supplierRecordIds.Any(id => id <= 0))
        {
            return new(InvitationOutcome.InvalidRequest);
        }

        // An invitation always creates an External user, so a staff address would
        // become External; staff sign in with Google instead (POST /api/session).
        if (internalDomains.Value.Contains(DomainOf(command.Email!)))
        {
            return new(InvitationOutcome.InternalEmail);
        }

        if (await roles.FindActiveRoleAsync(command.RoleId, cancellationToken) is not { } role)
        {
            return new(InvitationOutcome.RoleNotFound);
        }

        if (role.UserType != UserType.External)
        {
            return new(InvitationOutcome.RoleNotExternal);
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var (token, hash) = InvitationTokens.Generate();
        var invitation = Invitation.Create(command.Email!, role.Id, supplierRecordIds, hash, now + InvitationTokens.Lifetime, actorId, now);

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
                        roleId = role.Id,
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

        return new(InvitationOutcome.Success, await IssueAsync(invitation, role.Key, token, now, cancellationToken));
    }

    /// <summary>Issues a new token with a fresh 7-day expiry; the old link stops working.</summary>
    public async Task<InvitationResult> ResendAsync(int invitationId, CancellationToken cancellationToken)
    {
        if (await invitations.FindAsync(invitationId, cancellationToken) is not { } invitation)
        {
            return new(InvitationOutcome.NotFound);
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return new(InvitationOutcome.NotPending);
        }

        var actorId = await ActorIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var (token, hash) = InvitationTokens.Generate();
        invitation.Reissue(hash, now + InvitationTokens.Lifetime);
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.InvitationResent,
            UserId: actorId,
            Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, email = invitation.Email })));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var roleKey = await RoleKeyOfAsync(invitation.RoleId, cancellationToken);
        return new(InvitationOutcome.Success, await IssueAsync(invitation, roleKey, token, now, cancellationToken));
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
        Invitation invitation, string roleKey, string token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var acceptLink = options.Value.BuildAcceptLink(token);

        // The invitation is already committed: a failed email mustn't undo it.
        // The admin still gets the link to share by hand.
        var emailSent = true;
        try
        {
            await sender.SendAsync(new InvitationMessage(invitation.Id, invitation.Email, acceptLink, invitation.ExpiresAt), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            emailSent = false;
            logger.LogWarning(ex, "Invitation {InvitationId} was created but its email could not be sent.", invitation.Id);
        }

        return new IssuedInvitationDto(InvitationDto.From(invitation, roleKey, now), acceptLink, emailSent);
    }

    /// <summary>The route's permission check has already loaded the caller's access context.</summary>
    private async Task<int> ActorIdAsync(CancellationToken cancellationToken) =>
        (await currentAccess.GetAsync(cancellationToken))?.UserId
        ?? throw new InvalidOperationException("An authorized invitation request has no access context.");

    private async Task<string> RoleKeyOfAsync(int roleId, CancellationToken cancellationToken) =>
        (await roles.FindActiveRoleAsync(roleId, cancellationToken))?.Key ?? string.Empty;

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
