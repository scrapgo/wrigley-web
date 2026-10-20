namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// An invitation for an outside party to join as an External user with one
/// external role and one or more supplier links. Only the SHA-256 of the token
/// is stored. At most one <see cref="InvitationStatus.Pending"/> invitation
/// exists per email.
/// </summary>
public class Invitation
{
    private Invitation()
    {
    }

    public int Id { get; private set; }

    /// <summary>Lower-cased, so the pending-per-email rule and acceptance match case-insensitively.</summary>
    public string Email { get; private set; } = string.Empty;

    public int RoleId { get; private set; }

    /// <summary>The Quickbase Suppliers Record IDs the new user is linked to on acceptance.</summary>
    public List<int> SupplierRecordIds { get; private set; } = [];

    /// <summary>Hex SHA-256 of the one-time token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTimeOffset ExpiresAt { get; private set; }

    public int InvitedByUserId { get; private set; }

    public int? AcceptedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Invitation Create(
        string email,
        int roleId,
        IEnumerable<int> supplierRecordIds,
        string tokenHash,
        DateTimeOffset expiresAt,
        int invitedByUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new Invitation
        {
            Email = NormalizeEmail(email),
            RoleId = roleId,
            SupplierRecordIds = [.. supplierRecordIds.Distinct()],
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId,
            CreatedAt = now,
        };
    }

    public bool IsOpenAt(DateTimeOffset now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    /// <summary>Resending issues a new token, so the old link stops working.</summary>
    public void Reissue(string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public void Accept(int userId, DateTimeOffset now)
    {
        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = now;
    }

    public void Revoke() => Status = InvitationStatus.Revoked;

    public void Expire() => Status = InvitationStatus.Expired;
}
