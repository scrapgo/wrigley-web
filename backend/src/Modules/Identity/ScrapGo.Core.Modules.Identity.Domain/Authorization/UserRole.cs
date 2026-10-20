using ScrapGo.Core.Modules.Identity.Domain.Users;

namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Assigns a <see cref="Role"/> to a user. Keyed by (user, role). Both the
/// user and the role carry <see cref="UserType"/>: the composite foreign keys
/// <c>(user_id, user_type)</c> and <c>(role_id, user_type)</c> make the database
/// refuse a role held by a user of the other type.
/// </summary>
public class UserRole
{
    private UserRole()
    {
    }

    public int UserId { get; private set; }

    public int RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public UserType UserType { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Who assigned it; null when the system did (first sign-in, bootstrap, invitation).</summary>
    public int? AssignedByUserId { get; private set; }

    public static UserRole Assign(int userId, int roleId, UserType userType, int? assignedByUserId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            RoleId = roleId,
            UserType = userType,
            AssignedAt = now,
            AssignedByUserId = assignedByUserId,
        };
}
