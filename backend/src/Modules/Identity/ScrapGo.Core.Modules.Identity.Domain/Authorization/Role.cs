namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// A named set of <see cref="Permission"/>s. Four kinds, by
/// (<see cref="OrganizationId"/>, <see cref="ApplicationId"/>):
/// <list type="bullet">
/// <item>(null, null): a platform-defined built-in such as
/// <see cref="DefaultRoleNames.OrganizationAdministrator"/>, seeded by migration.</item>
/// <item>(org, null): an organization's custom role, managed by its admins.</item>
/// <item>(null, app): an application role <em>template</em>, platform-defined
/// (e.g. "{App} Administrator"), usable in every organization that has the app.</item>
/// <item>(org, app): an organization's custom role for one application,
/// managed by that application's administrators.</item>
/// </list>
/// An application role may hold only its own application's module permissions
/// and <see cref="Permissions.ApplicationManageAccess"/>.
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

    /// <summary>Null for a platform-scoped built-in role or an application role template.</summary>
    public int? OrganizationId { get; private set; }

    /// <summary>Set for an application role (template or custom); null for platform and organization roles.</summary>
    public int? ApplicationId { get; private set; }

    public RoleStatus Status { get; private set; } = RoleStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    public bool IsOrganizationScoped => OrganizationId is not null;

    public bool IsApplicationRole => ApplicationId is not null;

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

    /// <summary>
    /// An application role: an organization's custom role when
    /// <paramref name="organizationId"/> is set, or a platform-defined template
    /// when it's null.
    /// </summary>
    public static Role CreateForApplication(int? organizationId, int applicationId, string name, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            OrganizationId = organizationId,
            ApplicationId = applicationId,
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
