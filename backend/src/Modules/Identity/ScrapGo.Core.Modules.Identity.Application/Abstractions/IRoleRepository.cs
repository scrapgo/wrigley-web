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

    /// <summary>Every user holding the role, active or not.</summary>
    Task<IReadOnlyList<int>> ListHolderIdsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>True if the user holds the role.</summary>
    Task<bool> IsHeldByAsync(int userId, int roleId, CancellationToken cancellationToken);

    void AddUserRole(UserRole userRole);

    /// <summary>The names of the permissions a role grants, ordered by name. Empty for an unknown role.</summary>
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>Every active role the user holds, ordered by role name.</summary>
    Task<IReadOnlyList<AssignedRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>A tracked assignment, or null.</summary>
    Task<UserRole?> FindUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken);

    /// <summary>Stages removal of an assignment found by <see cref="FindUserRoleAsync"/>. Callers must invalidate the holder's permission cache after saving.</summary>
    void RemoveUserRole(UserRole userRole);
}
