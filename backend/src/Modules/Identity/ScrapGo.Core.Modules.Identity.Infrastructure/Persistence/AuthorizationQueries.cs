using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class AuthorizationQueries(IdentityDbContext dbContext) : IAuthorizationQueries
{
    public Task<bool> HasActiveMembershipAsync(int userId, int organizationId, CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active,
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
            .Select(ur => new { ur.UserId, Role = new AssignedRoleDto(ur.RoleId, ur.Role.Name, ur.OrganizationId) })
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
            .Where(r => (r.OrganizationId == organizationId || r.OrganizationId == null) && r.Status == RoleStatus.Active);

    public async Task<IReadOnlyList<string>> ListPermissionNamesAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
}
