using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// Read-only, untracked queries: those behind every access decision, and the
/// admin list reads, which project straight to DTOs.
/// </summary>
/// <remarks>
/// The list methods assume a valid <see cref="PageRequest"/> (check
/// <see cref="PageRequest.IsValid"/> first) and order by id, so pages are stable.
/// </remarks>
public interface IAuthorizationQueries
{
    Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Every permission granted by the user's active roles in exactly this
    /// scope. An organization-scoped lookup never sees platform-scoped grants,
    /// or another organization's, and vice versa.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken);

    /// <summary>Active organizations where the user has an active membership, ordered by id.</summary>
    Task<IReadOnlyList<OrganizationSummaryDto>> ListActiveOrganizationsAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Every user on the platform, filtered and paged. Platform-admin only.</summary>
    Task<PagedResult<UserSummaryDto>> ListUsersAsync(UserListFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every organization on the platform, filtered and paged. Platform-admin only.</summary>
    Task<PagedResult<OrganizationSummaryDto>> ListOrganizationsAsync(OrganizationListFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The organization with its active member count, or null if there is no such organization.</summary>
    Task<OrganizationDetailDto?> GetOrganizationDetailAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Active members of exactly this organization, paged by user id. Each
    /// member's roles are only their active roles in this same organization.
    /// </summary>
    Task<PagedResult<OrganizationMemberDto>> ListMembersAsync(int organizationId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// The roles usable in this organization: its own active custom roles plus
    /// the active built-in platform-defined roles. Never another organization's
    /// roles. Ordered platform built-ins first, then by name.
    /// </summary>
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// One role, if it is visible from this organization by the same rule as
    /// <see cref="ListRolesAsync"/>: active, and either this organization's own
    /// or a built-in. Null otherwise, including for another organization's role.
    /// </summary>
    Task<RoleDto?> FindRoleAsync(int organizationId, int roleId, CancellationToken cancellationToken);

    /// <summary>The whole seeded permission catalog, ordered by name.</summary>
    Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken);
}
