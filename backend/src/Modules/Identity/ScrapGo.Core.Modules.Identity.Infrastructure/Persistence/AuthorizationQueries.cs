using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class AuthorizationQueries(IdentityDbContext dbContext) : IAuthorizationQueries
{
    public Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active,
                cancellationToken);

    public async Task<bool> IsOrganizationAdministratorAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        await HasActiveMembershipAsync(userId, organizationId, cancellationToken)
        && await dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                ur => ur.UserId == userId
                    && ur.OrganizationId == organizationId
                    // Only the built-in, platform-defined role counts. A custom
                    // organization role that merely shares the name grants
                    // nothing here.
                    && ur.Role.OrganizationId == null
                    && ur.Role.Name == DefaultRoleNames.OrganizationAdministrator
                    && ur.Role.Status == RoleStatus.Active,
                cancellationToken);

    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken)
    {
        var userRoles = dbContext.UserRoles.AsNoTracking().Where(ur => ur.UserId == scope.UserId);

        // Written as two explicit branches rather than one nullable equality,
        // so the platform scope is unmistakably "OrganizationId IS NULL" and
        // can never match an organization-scoped grant.
        userRoles = scope.OrganizationId is { } organizationId
            ? userRoles.Where(ur => ur.OrganizationId == organizationId)
            : userRoles.Where(ur => ur.OrganizationId == null);

        var names = await userRoles
            .Where(ur => ur.Role.Status == RoleStatus.Active)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .Distinct()
            .ToListAsync(cancellationToken);

        return names.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<OrganizationSummaryDto>> ListActiveOrganizationsAsync(int userId, CancellationToken cancellationToken)
    {
        var organizations = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId
                && m.Status == MembershipStatus.Active
                && m.Organization.Status == OrganizationStatus.Active)
            .OrderBy(m => m.OrganizationId)
            .Select(m => new { m.Organization.Id, m.Organization.Name, m.Organization.Slug, m.Organization.Status })
            .ToListAsync(cancellationToken);

        return [.. organizations.Select(o => new OrganizationSummaryDto(o.Id, o.Name, o.Slug, o.Status.ToString()))];
    }

    public async Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
}
