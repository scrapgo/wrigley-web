namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// A named set of <see cref="Permission"/>s, held globally. The only built-in
/// is <see cref="DefaultRoleNames.PlatformAdministrator"/>, seeded by migration.
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

    public RoleStatus Status { get; private set; } = RoleStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    public static Role Create(string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
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
