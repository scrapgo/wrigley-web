namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IRoleRepository
{
    /// <summary>
    /// A tracked, active, organization-scoped role, or null. Platform-scoped
    /// built-in roles and soft-deleted roles are deliberately not editable
    /// through this path.
    /// </summary>
    Task<Role?> FindEditableOrganizationRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>The id of a built-in platform-scoped role, e.g. <see cref="DefaultRoleNames.OrganizationAdministrator"/>.</summary>
    Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken);

    void Add(Role role);

    void AddUserRole(UserRole userRole);

    /// <summary>True if any user role, in any scope, still references the role.</summary>
    Task<bool> HasAssignmentsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>Every distinct (user, scope) currently holding the role, for cache invalidation.</summary>
    Task<IReadOnlyList<PermissionScope>> GetAssignmentScopesAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>The catalog id for <paramref name="permissionName"/>, or null if it isn't in the seeded catalog.</summary>
    Task<int?> FindPermissionIdAsync(string permissionName, CancellationToken cancellationToken);

    /// <summary>A tracked grant, or null.</summary>
    Task<RolePermission?> FindRolePermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken);

    void AddRolePermission(RolePermission rolePermission);

    void RemoveRolePermission(RolePermission rolePermission);
}
