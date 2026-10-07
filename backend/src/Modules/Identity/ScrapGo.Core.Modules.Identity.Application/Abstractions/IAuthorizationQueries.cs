using ScrapGo.Core.Modules.Identity.Application.Applications;
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
    /// <summary>
    /// True only for an active membership in an <em>active</em> organization.
    /// A deactivated organization's members have no effective membership, so
    /// every organization- and application-scoped check denies them.
    /// </summary>
    Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the user's membership is active but the organization itself
    /// is deactivated. Lets the guard tell a member why they're denied, without
    /// telling a non-member anything.
    /// </summary>
    Task<bool> IsMemberOfDeactivatedOrganizationAsync(int userId, int organizationId, CancellationToken cancellationToken);

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

    /// <summary>The application catalog: every application, its modules and their permissions, ordered by id.</summary>
    Task<IReadOnlyList<CatalogApplicationDto>> ListCatalogAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The organization's active applications (assigned, and active in the
    /// catalog), each with its enabled, active modules only.
    /// </summary>
    Task<IReadOnlyList<OrganizationApplicationDto>> ListOrganizationApplicationsAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>True when the application is assigned to the organization (Active) and active in the catalog.</summary>
    Task<bool> IsApplicationAvailableAsync(int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// The application roles usable in (organization, application): its
    /// templates first, then the organization's custom roles, each with its
    /// permission names. Never another organization's roles.
    /// </summary>
    Task<IReadOnlyList<RoleDetailDto>> ListApplicationRolesAsync(int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>Unexpired application grants in (organization, application) held by active members, ordered by user id.</summary>
    Task<IReadOnlyList<ApplicationGrantHolder>> ListApplicationGrantHoldersAsync(
        int organizationId, int applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Every role the user holds in this organization, organization-level and
    /// application grants, expired ones included (with their expiry). Null if
    /// the user is not a member.
    /// </summary>
    Task<IReadOnlyList<AssignedRoleDto>?> ListMemberGrantsAsync(int organizationId, int userId, CancellationToken cancellationToken);

    /// <summary>The whole seeded permission catalog, ordered by name.</summary>
    Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken);
}
