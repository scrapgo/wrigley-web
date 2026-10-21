using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IRoleRepository
{
    /// <summary>An active role, untracked, or null.</summary>
    Task<Role?> FindActiveRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>The id of a built-in role, e.g. <see cref="DefaultRoleNames.SiteAdmin"/>.</summary>
    Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row lock on the role until the ambient transaction ends, so
    /// callers that check-then-assign it are serialized. Must run inside
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
    /// </summary>
    Task LockRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// How many <em>active</em> users hold the role, leaving out
    /// <paramref name="excludingUserId"/>. Disabled holders don't count: they
    /// can't act, so they can't administer anything.
    /// </summary>
    Task<int> CountActiveHoldersAsync(int roleId, int excludingUserId, CancellationToken cancellationToken);

    /// <summary>True if the user holds the role.</summary>
    Task<bool> IsHeldByAsync(int userId, int roleId, CancellationToken cancellationToken);

    void AddUserRole(UserRole userRole);

    /// <summary>Every role with its permission keys and holder count, internal roles first, then by name.</summary>
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken);

    /// <summary>One role as <see cref="ListRolesAsync"/> returns it, or null.</summary>
    Task<RoleDto?> GetRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>The whole permission catalog, grouped by owning feature (features by name, permissions by key).</summary>
    Task<IReadOnlyList<PermissionGroupDto>> ListPermissionGroupsAsync(CancellationToken cancellationToken);

    /// <summary>The catalog permissions with these keys, untracked; unknown keys are simply missing.</summary>
    Task<IReadOnlyList<Permission>> FindPermissionsByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

    /// <summary>The role's grants, tracked, with their <see cref="RolePermission.Permission"/>, for changing them.</summary>
    Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(int roleId, CancellationToken cancellationToken);

    void AddRolePermission(RolePermission rolePermission);

    void RemoveRolePermission(RolePermission rolePermission);

    /// <summary>Every role the user holds, ordered by role name.</summary>
    Task<IReadOnlyList<UserRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The user's role assignments, tracked, with their <see cref="UserRole.Role"/>, for changing them.</summary>
    Task<IReadOnlyList<UserRole>> ListUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The roles with these ids, untracked; ids with no role are simply missing from the result.</summary>
    Task<IReadOnlyList<Role>> FindRolesAsync(IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken);

    /// <summary>Stages removal of an assignment found by <see cref="ListUserRoleAssignmentsAsync"/>. Callers evict the holder's cached access after saving.</summary>
    void RemoveUserRole(UserRole userRole);
}
