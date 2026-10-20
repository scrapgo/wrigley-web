using ScrapGo.Core.Modules.Identity.Domain.Users;

namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// A named set of <see cref="Permission"/>s, held only by users of the same
/// <see cref="UserType"/>. V1 has three system roles (<c>site_admin</c>,
/// <c>scrapgo_user</c>, <c>user</c>), seeded by migration and never created or
/// deleted over HTTP. Code never checks a role's key; it checks permissions.
/// </summary>
public class Role
{
    private readonly List<RolePermission> _rolePermissions = [];

    private Role()
    {
    }

    public int Id { get; private set; }

    /// <summary>Stable, unique identifier such as <c>site_admin</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Who may hold the role. Part of <c>(id, user_type)</c>, the target of
    /// <c>user_roles</c>' composite foreign key.
    /// </summary>
    public UserType UserType { get; private set; }

    /// <summary>System roles are seeded and can't be created or deleted over HTTP.</summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    public static Role Create(string key, string name, string? description, UserType userType, bool isSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            Key = key.Trim(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            UserType = userType,
            IsSystem = isSystem,
        };
    }
}
