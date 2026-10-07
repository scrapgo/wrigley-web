using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Organizations;

/// <param name="ApplicationId">Set to pre-grant an application role (in that application); null for an organization-level role.</param>
public sealed record InvitationGrantRequest(int RoleId, int? ApplicationId);

/// <param name="ActorUid">The inviting admin's UID.</param>
/// <param name="ActingAsPlatformAdmin">A platform administrator creating an organization's first admin: no membership or escalation checks.</param>
/// <param name="ExpiresInDays">1 to 30; 7 when omitted.</param>
public sealed record CreateInvitationCommand(
    string ActorUid,
    bool ActingAsPlatformAdmin,
    int OrganizationId,
    string? Email,
    IReadOnlyList<InvitationGrantRequest> Grants,
    int? ExpiresInDays = null);

public enum InvitationOutcome
{
    Created,

    /// <summary>Not an email address, a duplicate grant, or an expiry outside 1–30 days.</summary>
    InvalidRequest,

    /// <summary>A pending invitation for this email already exists here.</summary>
    InvitationPending,

    /// <summary>A pre-grant names a role that isn't usable here (unknown, deleted, another organization's or application's, or PlatformAdministrator).</summary>
    RoleNotFound,

    /// <summary>A pre-grant names an application the organization doesn't have.</summary>
    ApplicationNotAssigned,

    /// <summary>The caller lacks <c>Role.Assign</c> (organization pre-grants) or <c>Application.ManageAccess</c> (application pre-grants).</summary>
    Forbidden,

    /// <summary>Escalation guard: a pre-granted role carries a permission the caller doesn't hold there.</summary>
    CannotGrantUnheldPermission,
}

/// <param name="Token">The one-time acceptance token. Shown only here, never stored or retrievable again.</param>
public sealed record CreatedInvitationDto(int InvitationId, string Token, DateTimeOffset ExpiresAt);

public sealed record CreateInvitationResult(InvitationOutcome Outcome, CreatedInvitationDto? Invitation = null);

public sealed record InvitationDto(int Id, string Email, string Status, DateTimeOffset ExpiresAt, IReadOnlyList<InvitationGrantRequest> Grants);

public enum AcceptInvitationOutcome
{
    Accepted,

    /// <summary>No open invitation with this token (unknown, accepted, revoked, or its organization is deactivated).</summary>
    NotFound,

    Expired,

    /// <summary>The token's email isn't verified by the identity provider.</summary>
    EmailNotVerified,

    /// <summary>The signed-in email isn't the invited one.</summary>
    EmailMismatch,
}

/// <param name="SkippedGrants">Pre-grants no longer valid at acceptance (role deleted, application removed); audited and skipped.</param>
public sealed record AcceptInvitationResult(AcceptInvitationOutcome Outcome, int? OrganizationId = null, int GrantedCount = 0, int SkippedGrants = 0);

/// <summary>
/// Invitations: granting access by email before the person's first sign-in
/// (ORG-APP-MODULE-MODEL.md section 8.2).
/// </summary>
/// <remarks>
/// <para>
/// Every pre-grant is validated at invite time with the same rules as a
/// direct grant: <c>Role.Assign</c> and the escalation guard for
/// organization-level roles, <c>Application.ManageAccess</c> and the
/// escalation guard (in that application) for application roles. Inviting
/// itself needs <c>User.Update</c> on the route.
/// </para>
/// <para>
/// Acceptance requires an identity-provider-verified email
/// (<c>email_verified</c>) equal to the invited one. It is one transaction:
/// provision the user if needed, add or reactivate the membership, apply each
/// still-valid pre-grant, mark the invitation accepted, and audit each step.
/// Delivering the token (email) is outside this API: the inviting admin passes
/// it on.
/// </para>
/// </remarks>
public sealed class InvitationService(
    IInvitationRepository invitations,
    IOrganizationRepository organizations,
    IRoleRepository roles,
    IUserRepository users,
    IAuthorizationQueries queries,
    PermissionResolver permissionResolver,
    ProvisionCurrentUserHandler provisionCurrentUser,
    IPermissionCache permissionCache,
    IUnitOfWork unitOfWork,
    IAuditLog<IdentityModule> auditLog,
    TimeProvider timeProvider)
{
    private const int DefaultExpiryDays = 7;
    private const int MaxExpiryDays = 30;

    public async Task<CreateInvitationResult> CreateAsync(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email)
            || !command.Email.Contains('@', StringComparison.Ordinal)
            || command.ExpiresInDays is < 1 or > MaxExpiryDays
            || command.Grants.DistinctBy(g => (g.RoleId, g.ApplicationId)).Count() != command.Grants.Count)
        {
            return new(InvitationOutcome.InvalidRequest);
        }

        var actorUserId = await users.GetIdByUidAsync(command.ActorUid, cancellationToken);
        foreach (var grant in command.Grants)
        {
            if (await CheckPreGrantAsync(command, actorUserId, grant, cancellationToken) is { } refused)
            {
                return new(refused);
            }
        }

        var now = timeProvider.GetUtcNow();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = now.AddDays(command.ExpiresInDays ?? DefaultExpiryDays);
        var email = Invitation.NormalizeEmail(command.Email);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await invitations.FindPendingAsync(command.OrganizationId, email, ct) is { } pending)
                {
                    if (pending.IsOpenAt(now))
                    {
                        return new CreateInvitationResult(InvitationOutcome.InvitationPending);
                    }

                    // An expired pending invitation would block the partial unique index; close it.
                    pending.Revoke(now);
                    await unitOfWork.SaveChangesAsync(ct);
                }

                var invitation = Invitation.Create(command.OrganizationId, email, HashToken(token), expiresAt, actorUserId, now);
                foreach (var grant in command.Grants)
                {
                    invitation.AddGrant(grant.RoleId, grant.ApplicationId);
                }

                invitations.Add(invitation);
                await unitOfWork.SaveChangesAsync(ct);

                auditLog.Record(new AuditEvent(
                    IdentityAuditEventTypes.InvitationCreated,
                    UserId: actorUserId,
                    OrganizationId: command.OrganizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        invitationId = invitation.Id,
                        email = MaskEmail(email),
                        grants = command.Grants,
                        expiresAt,
                    })));
                await unitOfWork.SaveChangesAsync(ct);

                return new CreateInvitationResult(InvitationOutcome.Created, new CreatedInvitationDto(invitation.Id, token, expiresAt));
            }, cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == IdentityUniqueConstraints.PendingInvitation)
        {
            return new(InvitationOutcome.InvitationPending);
        }
    }

    public async Task<IReadOnlyList<InvitationDto>> ListPendingAsync(int organizationId, CancellationToken cancellationToken) =>
        [.. (await invitations.ListPendingAsync(organizationId, cancellationToken)).Select(i => new InvitationDto(
            i.Id,
            i.EmailNormalized,
            i.IsOpenAt(timeProvider.GetUtcNow()) ? InvitationStatus.Pending.ToString() : "Expired",
            i.ExpiresAt,
            [.. i.Grants.Select(g => new InvitationGrantRequest(g.RoleId, g.ApplicationId))]))];

    /// <returns>False when there is no such invitation in this organization.</returns>
    public async Task<bool> RevokeAsync(string actorUid, int organizationId, int invitationId, CancellationToken cancellationToken)
    {
        var actorUserId = await users.GetIdByUidAsync(actorUid, cancellationToken);

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (await invitations.FindAsync(organizationId, invitationId, ct) is not { } invitation)
            {
                return false;
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return true;
            }

            invitation.Revoke(timeProvider.GetUtcNow());
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.InvitationRevoked,
                UserId: actorUserId,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { invitationId })));
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);
    }

    /// <param name="email">From the validated token's <c>email</c> claim.</param>
    /// <param name="emailVerified">From the validated token's <c>email_verified</c> claim.</param>
    /// <param name="hostedDomain">From the token's <c>hd</c> claim, used only if this also provisions the caller.</param>
    public async Task<AcceptInvitationResult> AcceptAsync(
        string identityPlatformUid, string? email, bool emailVerified, string? hostedDomain, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)
            || await invitations.FindByTokenHashAsync(HashToken(token), cancellationToken) is not { Status: InvitationStatus.Pending } invitation
            || await organizations.GetByIdAsync(invitation.OrganizationId, cancellationToken) is not { IsActive: true })
        {
            return new(AcceptInvitationOutcome.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        if (!invitation.IsOpenAt(now))
        {
            return new(AcceptInvitationOutcome.Expired);
        }

        if (!emailVerified)
        {
            return new(AcceptInvitationOutcome.EmailNotVerified);
        }

        if (string.IsNullOrWhiteSpace(email) || Invitation.NormalizeEmail(email) != invitation.EmailNormalized)
        {
            return new(AcceptInvitationOutcome.EmailMismatch);
        }

        var granted = new List<PermissionScope>();
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await provisionCurrentUser.HandleAsync(new ProvisionCurrentUserCommand(identityPlatformUid, email, hostedDomain), ct);
            var organizationId = invitation.OrganizationId;

            switch (await organizations.FindMembershipAsync(user.Id, organizationId, ct))
            {
                case { Status: MembershipStatus.Active }:
                    break;
                case { } disabled:
                    disabled.Enable(now);
                    RecordMembershipAdded(user.Id, organizationId, reactivated: true);
                    break;
                default:
                    organizations.AddMembership(OrganizationMembership.Create(user.Id, organizationId, now));
                    RecordMembershipAdded(user.Id, organizationId, reactivated: false);
                    break;
            }

            await unitOfWork.SaveChangesAsync(ct);

            var skipped = 0;
            foreach (var grant in invitation.Grants)
            {
                if (!await IsStillGrantableAsync(organizationId, grant, ct))
                {
                    skipped++;
                    continue;
                }

                if (await roles.FindUserRoleAsync(user.Id, grant.RoleId, organizationId, ct) is not null)
                {
                    continue;
                }

                roles.AddUserRole(grant.ApplicationId is { } applicationId
                    ? UserRole.AssignForApplication(user.Id, grant.RoleId, organizationId, applicationId, expiresAt: null, now)
                    : UserRole.Assign(user.Id, grant.RoleId, organizationId, now));
                granted.Add(new PermissionScope(user.Id, organizationId, grant.ApplicationId));
                auditLog.Record(new AuditEvent(
                    grant.ApplicationId is null ? IdentityAuditEventTypes.RoleAssigned : IdentityAuditEventTypes.AccessGranted,
                    UserId: invitation.InvitedByUserId,
                    OrganizationId: organizationId,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        targetUserId = user.Id,
                        roleId = grant.RoleId,
                        organizationId,
                        applicationId = grant.ApplicationId,
                        invitationId = invitation.Id,
                    })));
            }

            invitation.Accept(user.Id, now);
            auditLog.Record(new AuditEvent(
                IdentityAuditEventTypes.InvitationAccepted,
                UserId: user.Id,
                OrganizationId: organizationId,
                Metadata: JsonSerializer.Serialize(new { invitationId = invitation.Id, granted = granted.Count, skipped })));
            await unitOfWork.SaveChangesAsync(ct);

            return new AcceptInvitationResult(AcceptInvitationOutcome.Accepted, organizationId, granted.Count, skipped);
        }, cancellationToken);

        foreach (var scope in granted)
        {
            await permissionCache.InvalidateAsync(scope, cancellationToken);
        }

        return result;
    }

    private void RecordMembershipAdded(int userId, int organizationId, bool reactivated) =>
        auditLog.Record(new AuditEvent(
            IdentityAuditEventTypes.MembershipAdded,
            UserId: userId,
            OrganizationId: organizationId,
            Metadata: JsonSerializer.Serialize(new { targetUserId = userId, reactivated, viaInvitation = true })));

    /// <summary>Invite-time validation of one pre-grant: the same rules as a direct grant. Null when allowed.</summary>
    private async Task<InvitationOutcome?> CheckPreGrantAsync(
        CreateInvitationCommand command, int? actorUserId, InvitationGrantRequest grant, CancellationToken cancellationToken)
    {
        var organizationId = command.OrganizationId;

        if (grant.ApplicationId is { } applicationId)
        {
            if (!await queries.IsApplicationAvailableAsync(organizationId, applicationId, cancellationToken))
            {
                return InvitationOutcome.ApplicationNotAssigned;
            }

            if (await roles.FindApplicationRoleAsync(grant.RoleId, organizationId, applicationId, cancellationToken) is null)
            {
                return InvitationOutcome.RoleNotFound;
            }

            return command.ActingAsPlatformAdmin
                ? null
                : await CheckActorHoldsAsync(actorUserId, new PermissionScope(0, organizationId, applicationId),
                    Permissions.ApplicationManageAccess, grant.RoleId, cancellationToken);
        }

        if (await roles.FindActiveRoleAsync(grant.RoleId, cancellationToken) is not { IsApplicationRole: false } role
            || (role.OrganizationId is { } roleOrganizationId && roleOrganizationId != organizationId)
            || role.Id == await roles.GetPlatformRoleIdAsync(DefaultRoleNames.PlatformAdministrator, cancellationToken))
        {
            return InvitationOutcome.RoleNotFound;
        }

        return command.ActingAsPlatformAdmin
            ? null
            : await CheckActorHoldsAsync(actorUserId, new PermissionScope(0, organizationId), Permissions.RoleAssign, grant.RoleId, cancellationToken);
    }

    /// <summary>The caller must hold <paramref name="gatePermission"/> and every permission the role grants, in the scope.</summary>
    private async Task<InvitationOutcome?> CheckActorHoldsAsync(
        int? actorUserId, PermissionScope scopeTemplate, string gatePermission, int roleId, CancellationToken cancellationToken)
    {
        if (actorUserId is not { } actorId)
        {
            return InvitationOutcome.Forbidden;
        }

        var held = await permissionResolver.GetPermissionNamesAsync(scopeTemplate with { UserId = actorId }, cancellationToken);
        if (!held.Contains(gatePermission))
        {
            return InvitationOutcome.Forbidden;
        }

        return (await roles.GetRolePermissionsAsync(roleId, cancellationToken)).All(held.Contains)
            ? null
            : InvitationOutcome.CannotGrantUnheldPermission;
    }

    /// <summary>Acceptance re-check: the role is still active and, for an application grant, the application still assigned.</summary>
    private async Task<bool> IsStillGrantableAsync(int organizationId, InvitationGrant grant, CancellationToken cancellationToken) =>
        grant.ApplicationId is { } applicationId
            ? await queries.IsApplicationAvailableAsync(organizationId, applicationId, cancellationToken)
                && await roles.FindApplicationRoleAsync(grant.RoleId, organizationId, applicationId, cancellationToken) is not null
            : await roles.FindActiveRoleAsync(grant.RoleId, cancellationToken) is { IsApplicationRole: false };

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Audit rows keep the domain and a hint, not the full address.</summary>
    private static string MaskEmail(string email) =>
        email.IndexOf('@') is var at and > 0 ? $"{email[0]}***{email[at..]}" : "***";
}
