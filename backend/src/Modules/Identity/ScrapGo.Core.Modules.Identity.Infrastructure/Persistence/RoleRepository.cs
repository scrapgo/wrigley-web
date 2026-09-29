namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public Task<Role?> FindEditableOrganizationRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .Where(r => r.Id == roleId && r.OrganizationId != null && r.Status == RoleStatus.Active)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Name == roleName && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

    public void Add(Role role) => dbContext.Roles.Add(role);

    public void AddUserRole(UserRole userRole) => dbContext.UserRoles.Add(userRole);

    public Task<bool> HasAssignmentsAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<PermissionScope>> GetAssignmentScopesAsync(int roleId, CancellationToken cancellationToken)
    {
        var holders = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => new { ur.UserId, ur.OrganizationId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. holders.Select(h => new PermissionScope(h.UserId, h.OrganizationId))];
    }

    public Task<int?> FindPermissionIdAsync(string permissionName, CancellationToken cancellationToken) =>
        dbContext.Permissions
            .AsNoTracking()
            .Where(p => p.Name == permissionName)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<RolePermission?> FindRolePermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken) =>
        dbContext.RolePermissions
            .SingleOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);

    public void AddRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Add(rolePermission);

    public void RemoveRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Remove(rolePermission);
}
