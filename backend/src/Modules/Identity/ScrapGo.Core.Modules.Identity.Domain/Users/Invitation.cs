namespace ScrapGo.Core.Modules.Identity.Domain.Users;

/// <summary>
/// An invitation to join with preset roles. External invitations (outside
/// parties) carry external roles and one or more supplier links and are
/// accepted with the token. Internal invitations (ScrapGo staff) carry internal
/// roles and are applied at the invitee's first Google Workspace sign-in. Only
/// the SHA-256 of the token is stored. At most one
/// <see cref="InvitationStatus.Pending"/> invitation exists per email.
/// </summary>
public class Invitation
{
    private Invitation()
    {
    }

    public int Id { get; private set; }

    /// <summary>Lower-cased, so the pending-per-email rule and acceptance match case-insensitively.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>The user type the invitee joins as, fixed by the email's domain when the invitation is created.</summary>
    public UserType UserType { get; private set; } = UserType.External;

    /// <summary>The roles granted on acceptance, all of <see cref="UserType"/>.</summary>
    public List<int> RoleIds { get; private set; } = [];

    /// <summary>The Quickbase Suppliers Record IDs the new user is linked to on acceptance (external only).</summary>
    public List<int> SupplierRecordIds { get; private set; } = [];

    /// <summary>Hex SHA-256 of the one-time token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTimeOffset ExpiresAt { get; private set; }

    public int InvitedByUserId { get; private set; }

    public int? AcceptedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>How many times the invitee has asked for a new link since the last admin resend.</summary>
    public int RenewCount { get; private set; }

    public DateTimeOffset? LastRenewedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Invitation Create(
        string email,
        UserType userType,
        IEnumerable<int> roleIds,
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
            UserType = userType,
            RoleIds = [.. roleIds.Distinct()],
            SupplierRecordIds = [.. supplierRecordIds.Distinct()],
            TokenHash = tokenHash,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt,
            InvitedByUserId = invitedByUserId,
            CreatedAt = now,
        };
    }

    public bool IsOpenAt(DateTimeOffset now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    /// <summary>
    /// An admin resend: a new token, so the old link stops working, and the
    /// invitation is Pending again, with the invitee's renewals reset.
    /// </summary>
    public void Reissue(string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        Status = InvitationStatus.Pending;
        RenewCount = 0;
    }

    /// <summary>The invitee asked for a new link: reissued like a resend, but counted.</summary>
    public void Renew(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        Status = InvitationStatus.Pending;
        RenewCount++;
        LastRenewedAt = now;
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
