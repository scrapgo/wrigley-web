namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <param name="Email">The invitee's email; acceptance requires the signed-in email to match (case-insensitive).</param>
/// <param name="RoleId">Must be an external role.</param>
/// <param name="SupplierRecordIds">One or more Quickbase Suppliers Record IDs, linked on acceptance.</param>
public sealed record CreateInvitationCommand(string? Email, int RoleId, IReadOnlyList<int>? SupplierRecordIds);

public sealed record ListInvitationsQuery(string? Status, int? Page, int? PageSize);

/// <param name="Status">The effective status: a Pending invitation past its expiry reads as Expired.</param>
public sealed record InvitationDto(
    int Id,
    string Email,
    int RoleId,
    string RoleKey,
    IReadOnlyList<int> SupplierRecordIds,
    string Status,
    DateTimeOffset ExpiresAt,
    int InvitedByUserId,
    int? AcceptedByUserId,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset CreatedAt)
{
    public static InvitationDto From(Invitation invitation, string roleKey, DateTimeOffset now) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.RoleId,
            roleKey,
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

/// <summary>A role an invitation can grant: an external role.</summary>
public sealed record InvitableRoleDto(int Id, string Key, string Name, string? Description);

/// <summary>What create and resend return: the invitation and the link the admin can copy (it carries the token).</summary>
public sealed record IssuedInvitationDto(InvitationDto Invitation, string AcceptLink);

public enum InvitationOutcome
{
    Success,

    /// <summary>A malformed email, no supplier Record IDs, a non-positive id, or a bad page or status.</summary>
    InvalidRequest,

    /// <summary>No such active role.</summary>
    RoleNotFound,

    /// <summary>The role is internal; invitations are for external users only.</summary>
    RoleNotExternal,

    /// <summary>The email already has a pending invitation.</summary>
    AlreadyPending,

    NotFound,

    /// <summary>The invitation was accepted or revoked, so it can't be resent or revoked.</summary>
    NotPending,
}

public sealed record InvitationResult(InvitationOutcome Outcome, IssuedInvitationDto? Issued = null);
