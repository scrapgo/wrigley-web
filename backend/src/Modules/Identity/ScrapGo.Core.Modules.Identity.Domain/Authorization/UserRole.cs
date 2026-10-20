namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Assigns a <see cref="Role"/> to a user. Roles are global, so the grant applies everywhere.
/// </summary>
public class UserRole
{
    private UserRole()
    {
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    public int RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    /// <summary>When the grant stops resolving (time-boxed access, Decision 9). Null never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserRole Assign(int userId, int roleId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            RoleId = roleId,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;

    public void ChangeExpiry(DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        ExpiresAt = expiresAt;
        UpdatedAt = now;
    }
}
