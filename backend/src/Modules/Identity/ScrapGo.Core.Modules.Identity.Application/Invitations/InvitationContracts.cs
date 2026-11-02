namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <param name="Email">
/// The invitee's email; acceptance requires the signed-in email to match
/// (case-insensitive). An email on <c>INTERNAL_HD_ALLOWLIST</c> makes an
/// Internal invitation, any other an External one.
/// </param>
/// <param name="RoleIds">One or more active roles, all of the invitation's user type.</param>
/// <param name="SupplierRecordIds">External: one or more Quickbase Suppliers Record IDs, linked on acceptance. Internal: none.</param>
/// <param name="RequestOrigin">The portal the admin is using; links point back to it when it's a listed frontend origin.</param>
public sealed record CreateInvitationCommand(
    string? Email, IReadOnlyList<int>? RoleIds, IReadOnlyList<int>? SupplierRecordIds, string? RequestOrigin = null);

public sealed record ListInvitationsQuery(string? Status, int? Page, int? PageSize);

/// <param name="Status">The effective status: a Pending invitation past its expiry reads as Expired.</param>
public sealed record InvitationDto(
    int Id,
    string Email,
    string UserType,
    IReadOnlyList<int> RoleIds,
    IReadOnlyList<string> RoleKeys,
    IReadOnlyList<int> SupplierRecordIds,
    string Status,
    DateTimeOffset ExpiresAt,
    int InvitedByUserId,
    int? AcceptedByUserId,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset CreatedAt)
{
    /// <param name="roleKeys">The keys of <see cref="Invitation.RoleIds"/>, in any order.</param>
    public static InvitationDto From(Invitation invitation, IEnumerable<string> roleKeys, DateTimeOffset now) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.UserType.ToString(),
            [.. invitation.RoleIds.Order()],
            [.. roleKeys.Order(StringComparer.Ordinal)],
            [.. invitation.SupplierRecordIds.Order()],
            EffectiveStatus(invitation.Status, invitation.ExpiresAt, now).ToString(),
            invitation.ExpiresAt,
            invitation.InvitedByUserId,
            invitation.AcceptedByUserId,
            invitation.AcceptedAt,
            invitation.CreatedAt);

    public static InvitationStatus EffectiveStatus(InvitationStatus status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == InvitationStatus.Pending && expiresAt <= now ? InvitationStatus.Expired : status;
}

/// <summary>A role an invitation can grant. Internal roles go to internal emails, external roles to the rest.</summary>
/// <param name="RequiresUsersManage">Only callers holding <c>users.manage</c> can grant it (Site Admin).</param>
public sealed record InvitableRoleDto(int Id, string Key, string Name, string? Description, string UserType, bool RequiresUsersManage);

/// <summary>What the invite form needs: which email domains are internal, and the roles to offer.</summary>
public sealed record InvitationFormDto(IReadOnlyList<string> InternalDomains, IReadOnlyList<InvitableRoleDto> Roles);

/// <summary>What create and resend return: the invitation and the link the admin can copy (it carries the token).</summary>
/// <param name="EmailSent">
/// Whether the invitation email went out. False when sending failed (the
/// invitation still stands; the admin can copy the link instead).
/// </param>
public sealed record IssuedInvitationDto(InvitationDto Invitation, string AcceptLink, bool EmailSent);

public enum InvitationOutcome
{
    Success,

    /// <summary>
    /// A malformed email, no roles, a non-positive id, an external invitation
    /// without supplier Record IDs, an internal one with them, or a bad page or status.
    /// </summary>
    InvalidRequest,

    /// <summary>No such active role.</summary>
    RoleNotFound,

    /// <summary>
    /// A role of the other user type: internal emails (on <c>INTERNAL_HD_ALLOWLIST</c>)
    /// take internal roles only, every other email external roles only.
    /// </summary>
    RoleTypeMismatch,

    /// <summary>Granting Site Admin needs <c>users.manage</c>, which the caller lacks.</summary>
    SiteAdminRequiresUsersManage,

    /// <summary>The email already has a pending invitation.</summary>
    AlreadyPending,

    /// <summary>A user with this email already exists (any type or status); change their roles instead.</summary>
    UserExists,

    NotFound,

    /// <summary>The invitation was accepted or revoked, so it can't be resent or revoked.</summary>
    NotPending,
}

public sealed record InvitationResult(InvitationOutcome Outcome, IssuedInvitationDto? Issued = null);
