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

    /// <summary>The names of the permissions a role grants, ordered by name. Empty for an unknown role.</summary>
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Every permission key the user's roles grant, whatever the feature
    /// toggles say. What the escalation guard compares against: a toggle
    /// exposes a feature, it isn't part of what someone holds.
    /// </summary>
    Task<IReadOnlySet<string>> ListGrantedPermissionKeysAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Every permission key in the catalog, ordered.</summary>
    Task<IReadOnlyList<string>> ListPermissionKeysAsync(CancellationToken cancellationToken);

    /// <summary>Every role the user holds, ordered by role name.</summary>
    Task<IReadOnlyList<UserRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The user's role assignments, tracked, with their <see cref="UserRole.Role"/>, for changing them.</summary>
    Task<IReadOnlyList<UserRole>> ListUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The roles with these ids, untracked; ids with no role are simply missing from the result.</summary>
    Task<IReadOnlyList<Role>> FindRolesAsync(IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken);

    /// <summary>Stages removal of an assignment found by <see cref="ListUserRoleAssignmentsAsync"/>. Callers evict the holder's cached access after saving.</summary>
    void RemoveUserRole(UserRole userRole);
}
