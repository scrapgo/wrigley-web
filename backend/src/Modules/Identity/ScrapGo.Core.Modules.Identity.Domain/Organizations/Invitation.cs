namespace ScrapGo.Core.Modules.Identity.Domain.Organizations;

public enum InvitationStatus
{
    /// <summary>Waiting to be accepted (and not past <see cref="Invitation.ExpiresAt"/>).</summary>
    Pending,

    Accepted,

    Revoked,
}

/// <summary>
/// An admin's explicit invitation to join an organization, by email, with the
/// roles to grant on acceptance (pre-grants). It is the explicit grant
/// (deny-by-default holds): acceptance only binds it to the identity that
/// proves it owns the email (<c>email_verified</c>), and nothing is granted to
/// anyone without a matching invitation.
/// </summary>
/// <remarks>
/// Only a SHA-256 hash of the one-time token is stored; the token itself is
/// shown once, to the inviting admin, to pass on.
/// </remarks>
public class Invitation
{
    private readonly List<InvitationGrant> _grants = [];

    private Invitation()
    {
    }

    public int Id { get; private set; }

    public int OrganizationId { get; private set; }

    /// <summary>Trimmed, lower-cased.</summary>
    public string EmailNormalized { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Null when created by the system on a platform administrator's behalf without a user row.</summary>
    public int? InvitedByUserId { get; private set; }

    public int? AcceptedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<InvitationGrant> Grants => _grants;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Invitation Create(
        int organizationId, string email, string tokenHash, DateTimeOffset expiresAt, int? invitedByUserId, DateTimeOffset now) =>
        new()
        {
            OrganizationId = organizationId,
            EmailNormalized = NormalizeEmail(email),
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public bool IsOpenAt(DateTimeOffset now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    public void AddGrant(int roleId, int? applicationId) => _grants.Add(InvitationGrant.Create(roleId, applicationId));

    public void Accept(int userId, DateTimeOffset now)
    {
        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = now;
        UpdatedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = InvitationStatus.Revoked;
        UpdatedAt = now;
    }
}

/// <summary>One role to grant when the invitation is accepted: organization-level, or for one application.</summary>
public class InvitationGrant
{
    private InvitationGrant()
    {
    }

    public int Id { get; private set; }

    public int InvitationId { get; private set; }

    public int RoleId { get; private set; }

    /// <summary>Set for an application role grant.</summary>
    public int? ApplicationId { get; private set; }

    public static InvitationGrant Create(int roleId, int? applicationId) =>
        new() { RoleId = roleId, ApplicationId = applicationId };
}
