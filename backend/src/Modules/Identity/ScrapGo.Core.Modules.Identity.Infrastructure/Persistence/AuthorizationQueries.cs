using ScrapGo.Core.Modules.Identity.Application.Applications;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class AuthorizationQueries(IdentityDbContext dbContext, TimeProvider timeProvider) : IAuthorizationQueries
{
    public Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId
                    && m.OrganizationId == organizationId
                    && m.Status == MembershipStatus.Active
                    && m.Organization.Status == OrganizationStatus.Active,
                cancellationToken);

    public Task<bool> IsMemberOfDeactivatedOrganizationAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId
                    && m.OrganizationId == organizationId
                    && m.Status == MembershipStatus.Active
                    && m.Organization.Status != OrganizationStatus.Active,
                cancellationToken);

    /// <remarks>
    /// <para>
    /// Three explicit branches rather than nullable equalities, so each scope
    /// matches only its own grants: platform is "no organization, no
    /// application", organization is "this organization, no application", and
    /// application is "this organization and this application".
    /// </para>
    /// <para>
    /// Everywhere: the role is active, the grant is unexpired, and retired
    /// permissions never resolve. Organization scope also needs the
    /// organization active. Application scope needs everything in
    /// ORG-APP-MODULE-MODEL.md section 6.1: an active membership in an active
    /// organization, the application assigned and active in the catalog, and,
    /// for each module permission, its module active and enabled for this
    /// organization's application. Module-disabled resolves to deny whatever
    /// the role grants.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(PermissionScope scope, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var grants = dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == scope.UserId
                && ur.Role.Status == RoleStatus.Active
                && (ur.ExpiresAt == null || ur.ExpiresAt > now));

        IQueryable<Permission> permissions;
        if (scope.OrganizationId is not { } organizationId)
        {
            permissions = grants
                .Where(ur => ur.OrganizationId == null && ur.ApplicationId == null)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission));
        }
        else if (scope.ApplicationId is not { } applicationId)
        {
            permissions = grants
                .Where(ur => ur.OrganizationId == organizationId
                    && ur.ApplicationId == null
                    && dbContext.Organizations.Any(o => o.Id == organizationId && o.Status == OrganizationStatus.Active))
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission));
        }
        else
        {
            var organizationApplicationId = await dbContext.OrganizationApplications
                .AsNoTracking()
                .Where(oa => oa.OrganizationId == organizationId
                    && oa.ApplicationId == applicationId
                    && oa.Status == OrganizationApplicationStatus.Active
                    && dbContext.CatalogApplications.Any(a => a.Id == applicationId && a.Status == CatalogStatus.Active))
                .Select(oa => (int?)oa.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (organizationApplicationId is null
                || !await HasActiveMembershipAsync(scope.UserId, organizationId, cancellationToken))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var applicationScopeOnly = Permissions.ApplicationScopeOnly;
            permissions = grants
                .Where(ur => ur.OrganizationId == organizationId
                    && ur.ApplicationId == applicationId
                    && ur.Role.ApplicationId == applicationId)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission))
                .Where(p => (p.ModuleId == null && applicationScopeOnly.Contains(p.Name))
                    || (dbContext.CatalogModules.Any(m =>
                            m.Id == p.ModuleId && m.ApplicationId == applicationId && m.Status == CatalogStatus.Active)
                        && dbContext.OrganizationApplicationModules.Any(oam =>
                            oam.OrganizationApplicationId == organizationApplicationId
                            && oam.ModuleId == p.ModuleId
                            && oam.Status == ModuleEnablementStatus.Enabled)));
        }

        var retired = Permissions.Retired;
        var names = await permissions
            .Select(p => p.Name)
            // Retired permissions never resolve, even if a stale grant survives.
            .Where(name => !retired.Contains(name))
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

    public async Task<PagedResult<UserSummaryDto>> ListUsersAsync(
        UserListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();

        if (filter.Status is { } status)
        {
            users = users.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            users = users.Where(u => EF.Functions.ILike(u.Email, pattern, LikePatterns.EscapeCharacter));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var rows = await users
            .OrderBy(u => u.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .Select(u => new { u.Id, u.Email, u.Status, u.CreatedAt })
            .ToListAsync(cancellationToken);

        return PagedResult<UserSummaryDto>.Create(
            [.. rows.Select(u => new UserSummaryDto(u.Id, u.Email, DisplayName: null, u.Status.ToString(), u.CreatedAt))], page, totalCount);
    }

    public async Task<PagedResult<OrganizationSummaryDto>> ListOrganizationsAsync(
        OrganizationListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var organizations = dbContext.Organizations.AsNoTracking();

        if (filter.Status is { } status)
        {
            organizations = organizations.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePatterns.Contains(filter.Search.Trim());
            organizations = organizations.Where(o =>
                EF.Functions.ILike(o.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(o.Slug, pattern, LikePatterns.EscapeCharacter));
        }

        var totalCount = await organizations.CountAsync(cancellationToken);
        var rows = await organizations
            .OrderBy(o => o.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .Select(o => new { o.Id, o.Name, o.Slug, o.Status })
            .ToListAsync(cancellationToken);

        return PagedResult<OrganizationSummaryDto>.Create(
            [.. rows.Select(o => new OrganizationSummaryDto(o.Id, o.Name, o.Slug, o.Status.ToString()))], page, totalCount);
    }

    public async Task<OrganizationDetailDto?> GetOrganizationDetailAsync(int organizationId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.Slug,
                o.Status,
                o.CreatedAt,
                ActiveMemberCount = dbContext.OrganizationMemberships.Count(
                    m => m.OrganizationId == o.Id && m.Status == MembershipStatus.Active),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new OrganizationDetailDto(row.Id, row.Name, row.Slug, row.Status.ToString(), row.CreatedAt, row.ActiveMemberCount);
    }

    public async Task<PagedResult<OrganizationMemberDto>> ListMembersAsync(
        int organizationId, PageRequest page, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Membership has no User navigation (by design), hence the explicit join.
        var members =
            from m in dbContext.OrganizationMemberships.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            where m.OrganizationId == organizationId && m.Status == MembershipStatus.Active
            select new { u.Id, u.Email, JoinedAt = m.CreatedAt };

        var totalCount = await members.CountAsync(cancellationToken);
        var rows = await members
            .OrderBy(m => m.Id)
            .Skip(page.Skip)
            .Take(page.ResolvedPageSize)
            .ToListAsync(cancellationToken);

        var userIds = rows.Select(r => r.Id).ToList();

        // Roles are filtered to this same organization at the query level; a
        // member's roles in any other organization or at platform scope never load.
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.OrganizationId == organizationId
                && userIds.Contains(ur.UserId)
                && ur.Role.Status == RoleStatus.Active)
            .OrderBy(ur => ur.Role.Name)
            .Where(ur => ur.ExpiresAt == null || ur.ExpiresAt > now)
            .Select(ur => new { ur.UserId, Role = new AssignedRoleDto(ur.RoleId, ur.Role.Name, ur.OrganizationId, ur.ApplicationId, ur.ExpiresAt) })
            .ToListAsync(cancellationToken);

        var rolesByUser = roles.ToLookup(r => r.UserId, r => r.Role);

        return PagedResult<OrganizationMemberDto>.Create(
            [.. rows.Select(r => new OrganizationMemberDto(r.Id, r.Email, null, [.. rolesByUser[r.Id]], r.JoinedAt))],
            page,
            totalCount);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(int organizationId, CancellationToken cancellationToken) =>
        await RolesVisibleFrom(organizationId)
            .OrderBy(r => r.OrganizationId != null)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.OrganizationId))
            .ToListAsync(cancellationToken);

    public Task<RoleDto?> FindRoleAsync(int organizationId, int roleId, CancellationToken cancellationToken) =>
        RolesVisibleFrom(organizationId)
            .Where(r => r.Id == roleId)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>The one visibility rule for role reads: active, and this organization's own or a built-in.</summary>
    private IQueryable<Role> RolesVisibleFrom(int organizationId) =>
        dbContext.Roles
            .AsNoTracking()
            // Organization-level roles only: application roles are listed per application.
            .Where(r => (r.OrganizationId == organizationId || r.OrganizationId == null)
                && r.ApplicationId == null
                && r.Status == RoleStatus.Active);

    public async Task<IReadOnlyList<CatalogApplicationDto>> ListCatalogAsync(CancellationToken cancellationToken)
    {
        var applications = await dbContext.CatalogApplications.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var modules = await dbContext.CatalogModules.AsNoTracking().OrderBy(m => m.Id).ToListAsync(cancellationToken);
        var permissions = await dbContext.Permissions.AsNoTracking()
            .Where(p => p.ModuleId != null)
            .Select(p => new { p.Name, ModuleId = p.ModuleId!.Value })
            .ToListAsync(cancellationToken);

        var permissionsByModule = permissions.ToLookup(p => p.ModuleId, p => p.Name);
        var modulesByApplication = modules.ToLookup(m => m.ApplicationId);

        return [.. applications.Select(a => new CatalogApplicationDto(
            a.Id,
            a.Key,
            a.Name,
            a.Status.ToString(),
            [.. modulesByApplication[a.Id].Select(m => new CatalogModuleDto(
                m.Id, m.Key, m.Name, m.Status.ToString(), [.. permissionsByModule[m.Id].Order(StringComparer.Ordinal)]))]))];
    }

    public async Task<IReadOnlyList<OrganizationApplicationDto>> ListOrganizationApplicationsAsync(
        int organizationId, CancellationToken cancellationToken)
    {
        var applications = await (
            from oa in dbContext.OrganizationApplications.AsNoTracking()
            join a in dbContext.CatalogApplications.AsNoTracking() on oa.ApplicationId equals a.Id
            where oa.OrganizationId == organizationId
                && oa.Status == OrganizationApplicationStatus.Active
                && a.Status == CatalogStatus.Active
            orderby a.Id
            select new { OrganizationApplicationId = oa.Id, a.Id, a.Key, a.Name, oa.EnabledAt })
            .ToListAsync(cancellationToken);

        var organizationApplicationIds = applications.Select(a => a.OrganizationApplicationId).ToList();
        var modules = await (
            from oam in dbContext.OrganizationApplicationModules.AsNoTracking()
            join m in dbContext.CatalogModules.AsNoTracking() on oam.ModuleId equals m.Id
            where organizationApplicationIds.Contains(oam.OrganizationApplicationId)
                && oam.Status == ModuleEnablementStatus.Enabled
                && m.Status == CatalogStatus.Active
            orderby m.Id
            select new { oam.OrganizationApplicationId, m.Id, m.Key, m.Name })
            .ToListAsync(cancellationToken);

        var modulesByApplication = modules.ToLookup(m => m.OrganizationApplicationId);

        return [.. applications.Select(a => new OrganizationApplicationDto(
            a.Id,
            a.Key,
            a.Name,
            a.EnabledAt,
            [.. modulesByApplication[a.OrganizationApplicationId].Select(m => new OrganizationModuleDto(m.Id, m.Key, m.Name))]))];
    }

    public Task<bool> IsApplicationAvailableAsync(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationApplications
            .AsNoTracking()
            .AnyAsync(oa => oa.OrganizationId == organizationId
                && oa.ApplicationId == applicationId
                && oa.Status == OrganizationApplicationStatus.Active
                && dbContext.CatalogApplications.Any(a => a.Id == applicationId && a.Status == CatalogStatus.Active),
                cancellationToken);

    public async Task<IReadOnlyList<RoleDetailDto>> ListApplicationRolesAsync(
        int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId
                && (r.OrganizationId == null || r.OrganizationId == organizationId)
                && r.Status == RoleStatus.Active)
            .OrderBy(r => r.OrganizationId != null)
            .ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.OrganizationId,
                r.ApplicationId,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Name).ToList(),
            })
            .ToListAsync(cancellationToken);

        return [.. roles.Select(r => new RoleDetailDto(
            r.Id, r.Name, r.Description, r.OrganizationId, [.. r.Permissions.Order(StringComparer.Ordinal)], r.ApplicationId))];
    }

    public async Task<IReadOnlyList<ApplicationGrantHolder>> ListApplicationGrantHoldersAsync(
        int organizationId, int applicationId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await (
            from ur in dbContext.UserRoles.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
            where ur.OrganizationId == organizationId
                && ur.ApplicationId == applicationId
                && (ur.ExpiresAt == null || ur.ExpiresAt > now)
                && ur.Role.Status == RoleStatus.Active
                && u.Status == UserStatus.Active
                && dbContext.OrganizationMemberships.Any(m =>
                    m.UserId == ur.UserId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active)
            orderby ur.UserId, ur.Role.Name
            select new { ur.UserId, u.Email, ur.RoleId, RoleName = ur.Role.Name, ur.ExpiresAt })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ApplicationGrantHolder(
            r.UserId, r.Email, new AssignedRoleDto(r.RoleId, r.RoleName, organizationId, applicationId, r.ExpiresAt)))];
    }

    public async Task<IReadOnlyList<AssignedRoleDto>?> ListMemberGrantsAsync(int organizationId, int userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.OrganizationMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken))
        {
            return null;
        }

        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.OrganizationId == organizationId && ur.Role.Status == RoleStatus.Active)
            .OrderBy(ur => ur.ApplicationId != null)
            .ThenBy(ur => ur.ApplicationId)
            .ThenBy(ur => ur.Role.Name)
            .Select(ur => new AssignedRoleDto(ur.RoleId, ur.Role.Name, ur.OrganizationId, ur.ApplicationId, ur.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .Where(p => !Permissions.Retired.Contains(p.Name))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
}
