namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Assigns a <see cref="Role"/> to a user, in one scope:
/// <list type="bullet">
/// <item><see cref="OrganizationId"/> and <see cref="ApplicationId"/> null: a platform-scoped assignment.</item>
/// <item><see cref="OrganizationId"/> set: the role applies only inside that organization.</item>
/// <item>Both set: an application grant, applying only to that application in
/// that organization, and only to its enabled modules.</item>
/// </list>
/// Permissions never leak across scopes: a grant in organization A says
/// nothing about organization B, and a grant for application A nothing about B.
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

    /// <summary>Null for a platform-scoped assignment.</summary>
    public int? OrganizationId { get; private set; }

    /// <summary>Set for an application grant; always the role's own application.</summary>
    public int? ApplicationId { get; private set; }

    /// <summary>When the grant stops resolving (time-boxed access, Decision 9). Null never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserRole Assign(int userId, int roleId, int? organizationId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            RoleId = roleId,
            OrganizationId = organizationId,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public static UserRole AssignForApplication(
        int userId, int roleId, int organizationId, int applicationId, DateTimeOffset? expiresAt, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            RoleId = roleId,
            OrganizationId = organizationId,
            ApplicationId = applicationId,
            ExpiresAt = expiresAt,
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
