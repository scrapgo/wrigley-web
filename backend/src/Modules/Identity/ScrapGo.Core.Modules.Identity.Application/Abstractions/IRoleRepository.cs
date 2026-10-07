using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

public interface IRoleRepository
{
    /// <summary>
    /// A tracked, active, organization-level custom role, or null. Built-in
    /// roles, application roles and soft-deleted roles are deliberately not
    /// editable through this path.
    /// </summary>
    Task<Role?> FindEditableOrganizationRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// An active application role usable in (organization, application): the
    /// application's templates or the organization's own custom roles for it.
    /// Untracked; null for anything else, including another organization's role.
    /// </summary>
    Task<Role?> FindApplicationRoleAsync(int roleId, int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>A tracked, active custom application role of exactly this organization and application (templates aren't editable), or null.</summary>
    Task<Role?> FindEditableApplicationRoleAsync(int roleId, int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// The catalog id of a permission an application role of
    /// <paramref name="applicationId"/> may hold: one of that application's
    /// module permissions, or an application-scope identity permission
    /// (<c>Application.ManageAccess</c>). Null otherwise, including retired ones.
    /// </summary>
    Task<int?> FindApplicationPermissionIdAsync(string permissionName, int applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// How many other active members hold <c>Application.ManageAccess</c> in
    /// (organization, application) through an unexpired grant.
    /// </summary>
    Task<int> CountOtherApplicationAdministratorsAsync(
        int organizationId, int applicationId, int excludingUserId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Every grant for (organization, application), tracked, for the application-removal cascade.</summary>
    Task<IReadOnlyList<UserRole>> ListApplicationGrantsAsync(int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>An active role in any scope (organization or built-in), untracked, or null.</summary>
    Task<Role?> FindActiveRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>The id of a built-in platform-scoped role, e.g. <see cref="DefaultRoleNames.OrganizationAdministrator"/>.</summary>
    Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a row lock on the role until the ambient transaction ends, so
    /// callers that check-then-assign it are serialized. Must run inside
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
    /// </summary>
    Task LockRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// How many <em>active</em> users hold the role at platform scope, leaving
    /// out <paramref name="excludingUserId"/>. Disabled holders don't count:
    /// they can't act, so they can't administer the platform.
    /// </summary>
    Task<int> CountActivePlatformHoldersAsync(int roleId, int excludingUserId, CancellationToken cancellationToken);

    /// <summary>
    /// How many <em>active</em> users with an <em>active</em> membership in the
    /// organization hold the role there, leaving out <paramref name="excludingUserId"/>.
    /// </summary>
    Task<int> CountActiveOrganizationHoldersAsync(int roleId, int organizationId, int excludingUserId, CancellationToken cancellationToken);

    /// <summary>Every assignment the user holds in this organization, tracked, for removal.</summary>
    Task<IReadOnlyList<UserRole>> ListUserRoleAssignmentsAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>True if the user holds the role in exactly this scope (null = platform scope).</summary>
    Task<bool> IsHeldByAsync(int userId, int roleId, int? organizationId, CancellationToken cancellationToken);

    void Add(Role role);

    void AddUserRole(UserRole userRole);

    /// <summary>True if any user role, in any scope, still references the role.</summary>
    Task<bool> HasAssignmentsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>Every distinct (user, scope) currently holding the role, for cache invalidation.</summary>
    Task<IReadOnlyList<PermissionScope>> GetAssignmentScopesAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// The catalog id of a permission an <em>organization-level</em> role may
    /// hold, or null: unknown names, module permissions (they belong in
    /// application roles) and application-scope-only permissions all return null.
    /// </summary>
    Task<int?> FindPermissionIdAsync(string permissionName, CancellationToken cancellationToken);

    /// <summary>A tracked grant, or null.</summary>
    Task<RolePermission?> FindRolePermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken);

    /// <summary>The names of the permissions a role grants, ordered by name. Empty for an unknown role.</summary>
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Every active role the user holds, in every scope (platform and each
    /// organization), ordered by organization (platform first) then role name.
    /// </summary>
    Task<IReadOnlyList<AssignedRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>A tracked assignment in exactly this scope (null = platform scope), or null.</summary>
    Task<UserRole?> FindUserRoleAsync(int userId, int roleId, int? organizationId, CancellationToken cancellationToken);

    /// <summary>Stages removal of an assignment found by <see cref="FindUserRoleAsync"/>. Callers must invalidate the holder's permission cache after saving.</summary>
    void RemoveUserRole(UserRole userRole);

    void AddRolePermission(RolePermission rolePermission);

    void RemoveRolePermission(RolePermission rolePermission);
}
