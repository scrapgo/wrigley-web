namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// A named set of <see cref="Permission"/>s. A role is either
/// organization-scoped (<see cref="OrganizationId"/> set: a custom role an
/// organization administrator manages) or platform-scoped
/// (<see cref="OrganizationId"/> null: a built-in role such as
/// <see cref="DefaultRoleNames.OrganizationAdministrator"/>, seeded by migration).
/// </summary>
/// <remarks>Roles are soft-deleted (<see cref="RoleStatus.Deleted"/>), never removed.</remarks>
public class Role
{
    private readonly List<RolePermission> _rolePermissions = [];

    private Role()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Null for a platform-scoped built-in role.</summary>
    public int? OrganizationId { get; private set; }

    public RoleStatus Status { get; private set; } = RoleStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    public bool IsOrganizationScoped => OrganizationId is not null;

    public static Role CreateForOrganization(int organizationId, string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Status = RoleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// A platform-scoped role. Its assignments carry no organization, and only
    /// they satisfy platform-scoped permission checks.
    /// </summary>
    public static Role CreatePlatformRole(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            OrganizationId = null,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Status = RoleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Rename(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        UpdatedAt = now;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        Status = RoleStatus.Deleted;
        UpdatedAt = now;
    }
}
