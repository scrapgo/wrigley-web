using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public Task<Role?> FindEditableOrganizationRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .Where(r => r.Id == roleId && r.OrganizationId != null && r.ApplicationId == null && r.Status == RoleStatus.Active)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Role?> FindApplicationRoleAsync(int roleId, int organizationId, int applicationId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == roleId
                && r.ApplicationId == applicationId
                && (r.OrganizationId == null || r.OrganizationId == organizationId)
                && r.Status == RoleStatus.Active,
                cancellationToken);

    public Task<Role?> FindEditableApplicationRoleAsync(int roleId, int organizationId, int applicationId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .SingleOrDefaultAsync(r => r.Id == roleId
                && r.ApplicationId == applicationId
                && r.OrganizationId == organizationId
                && r.Status == RoleStatus.Active,
                cancellationToken);

    public Task<int?> FindApplicationPermissionIdAsync(string permissionName, int applicationId, CancellationToken cancellationToken)
    {
        var applicationScopeOnly = Permissions.ApplicationScopeOnly;
        var retired = Permissions.Retired;

        return dbContext.Permissions
            .AsNoTracking()
            .Where(p => p.Name == permissionName && !retired.Contains(p.Name))
            .Where(p => (p.ModuleId == null && applicationScopeOnly.Contains(p.Name))
                || dbContext.CatalogModules.Any(m => m.Id == p.ModuleId && m.ApplicationId == applicationId))
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountOtherApplicationAdministratorsAsync(
        int organizationId, int applicationId, int excludingUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (from ur in dbContext.UserRoles.AsNoTracking()
         join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
         where ur.OrganizationId == organizationId
             && ur.ApplicationId == applicationId
             && ur.UserId != excludingUserId
             && (ur.ExpiresAt == null || ur.ExpiresAt > now)
             && ur.Role.Status == RoleStatus.Active
             && ur.Role.RolePermissions.Any(rp => rp.Permission.Name == Permissions.ApplicationManageAccess)
             && u.Status == UserStatus.Active
             && dbContext.OrganizationMemberships.Any(m =>
                 m.UserId == ur.UserId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active)
         select ur.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRole>> ListApplicationGrantsAsync(int organizationId, int applicationId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.OrganizationId == organizationId && ur.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);

    public Task<Role?> FindActiveRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == roleId && r.Status == RoleStatus.Active, cancellationToken);

    public Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Name == roleName && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

    public Task LockRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.roles WHERE id = {roleId} FOR UPDATE", cancellationToken);

    public Task<int> CountActivePlatformHoldersAsync(int roleId, int excludingUserId, CancellationToken cancellationToken) =>
        (from ur in dbContext.UserRoles.AsNoTracking()
         join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
         where ur.RoleId == roleId
             && ur.OrganizationId == null
             && ur.UserId != excludingUserId
             && u.Status == UserStatus.Active
         select ur.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public Task<int> CountActiveOrganizationHoldersAsync(
        int roleId, int organizationId, int excludingUserId, CancellationToken cancellationToken) =>
        (from ur in dbContext.UserRoles.AsNoTracking()
         join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
         where ur.RoleId == roleId
             && ur.OrganizationId == organizationId
             && ur.UserId != excludingUserId
             && u.Status == UserStatus.Active
             && dbContext.OrganizationMemberships.Any(m =>
                 m.UserId == ur.UserId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active)
         select ur.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRole>> ListUserRoleAssignmentsAsync(
        int userId, int organizationId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    public Task<bool> IsHeldByAsync(int userId, int roleId, int? organizationId, CancellationToken cancellationToken)
    {
        var userRoles = dbContext.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId && ur.RoleId == roleId);

        userRoles = organizationId is { } id
            ? userRoles.Where(ur => ur.OrganizationId == id)
            : userRoles.Where(ur => ur.OrganizationId == null);

        return userRoles.AnyAsync(cancellationToken);
    }

    public void Add(Role role) => dbContext.Roles.Add(role);

    public void AddUserRole(UserRole userRole) => dbContext.UserRoles.Add(userRole);

    public Task<bool> HasAssignmentsAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<PermissionScope>> GetAssignmentScopesAsync(int roleId, CancellationToken cancellationToken)
    {
        var holders = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => new { ur.UserId, ur.OrganizationId, ur.ApplicationId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. holders.Select(h => new PermissionScope(h.UserId, h.OrganizationId, h.ApplicationId))];
    }

    public Task<int?> FindPermissionIdAsync(string permissionName, CancellationToken cancellationToken)
    {
        var applicationScopeOnly = Permissions.ApplicationScopeOnly;

        return dbContext.Permissions
            .AsNoTracking()
            .Where(p => p.Name == permissionName && p.ModuleId == null && !applicationScopeOnly.Contains(p.Name))
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<RolePermission?> FindRolePermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken) =>
        dbContext.RolePermissions
            .SingleOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken) =>
        await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssignedRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.Status == RoleStatus.Active)
            // Postgres sorts NULL last in ascending order, so put platform
            // scope first, and organization-level before application grants,
            // explicitly.
            .OrderBy(ur => ur.OrganizationId != null)
            .ThenBy(ur => ur.OrganizationId)
            .ThenBy(ur => ur.ApplicationId != null)
            .ThenBy(ur => ur.ApplicationId)
            .ThenBy(ur => ur.Role.Name)
            .Select(ur => new AssignedRoleDto(ur.RoleId, ur.Role.Name, ur.OrganizationId, ur.ApplicationId, ur.ExpiresAt))
            .ToListAsync(cancellationToken);

    public Task<UserRole?> FindUserRoleAsync(int userId, int roleId, int? organizationId, CancellationToken cancellationToken)
    {
        var userRoles = dbContext.UserRoles.Where(ur => ur.UserId == userId && ur.RoleId == roleId);

        userRoles = organizationId is { } id
            ? userRoles.Where(ur => ur.OrganizationId == id)
            : userRoles.Where(ur => ur.OrganizationId == null);

        return userRoles.SingleOrDefaultAsync(cancellationToken);
    }

    public void RemoveUserRole(UserRole userRole) => dbContext.UserRoles.Remove(userRole);

    public void AddRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Add(rolePermission);

    public void RemoveRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Remove(rolePermission);
}
