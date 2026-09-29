using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>Read-only, untracked queries behind every access decision.</summary>
public interface IAuthorizationQueries
{
    Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// True only if the user has an active membership in the organization and
    /// holds <see cref="DefaultRoleNames.OrganizationAdministrator"/> there.
    /// </summary>
    Task<bool> IsOrganizationAdministratorAsync(int userId, int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Every permission granted by the user's active roles in exactly this
    /// scope. An organization-scoped lookup never sees platform-scoped grants,
    /// or another organization's, and vice versa.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken);

    /// <summary>Active organizations where the user has an active membership, ordered by id.</summary>
    Task<IReadOnlyList<OrganizationSummaryDto>> ListActiveOrganizationsAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The whole seeded permission catalog, ordered by name.</summary>
    Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken);
}
