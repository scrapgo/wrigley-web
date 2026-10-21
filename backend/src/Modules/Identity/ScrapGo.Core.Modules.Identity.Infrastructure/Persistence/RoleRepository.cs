using ScrapGo.Core.Modules.Identity.Application.Roles;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public Task<Role?> FindActiveRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public Task<int> GetPlatformRoleIdAsync(string roleKey, CancellationToken cancellationToken) =>
        dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Key == roleKey)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

    public Task LockRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.roles WHERE id = {roleId} FOR UPDATE", cancellationToken);

    public Task<int> CountActiveHoldersAsync(int roleId, int excludingUserId, CancellationToken cancellationToken) =>
        (from ur in dbContext.UserRoles.AsNoTracking()
         join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
         where ur.RoleId == roleId
             && ur.UserId != excludingUserId
             && u.Status == UserStatus.Active
         select ur.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public Task<bool> IsHeldByAsync(int userId, int roleId, CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

    public void AddUserRole(UserRole userRole) => dbContext.UserRoles.Add(userRole);

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var rows = await QueryRoles(dbContext.Roles.AsNoTracking()).ToListAsync(cancellationToken);

        return [.. rows.OrderBy(row => row.UserType).ThenBy(row => row.Name, StringComparer.Ordinal).Select(ToDto)];
    }

    public async Task<RoleDto?> GetRoleAsync(int roleId, CancellationToken cancellationToken) =>
        await QueryRoles(dbContext.Roles.AsNoTracking().Where(r => r.Id == roleId)).SingleOrDefaultAsync(cancellationToken) is { } row
            ? ToDto(row)
            : null;

    public async Task<IReadOnlyList<PermissionGroupDto>> ListPermissionGroupsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from p in dbContext.Permissions.AsNoTracking()
            join f in dbContext.Features.AsNoTracking() on p.FeatureId equals f.Id
            select new { FeatureKey = f.Key, FeatureName = f.Name, Permission = p })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .GroupBy(row => (row.FeatureKey, row.FeatureName))
                .OrderBy(group => group.Key.FeatureName, StringComparer.Ordinal)
                .Select(group => new PermissionGroupDto(
                    group.Key.FeatureKey,
                    group.Key.FeatureName,
                    [
                        .. group
                            .Select(row => row.Permission)
                            .OrderBy(p => p.Key, StringComparer.Ordinal)
                            .Select(p => new PermissionDto(p.Id, p.Key, p.Name, p.Description, p.Audience.ToString())),
                    ])),
        ];
    }

    public async Task<IReadOnlyList<Permission>> FindPermissionsByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
        await dbContext.Permissions.AsNoTracking().Where(p => keys.Contains(p.Key)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(int roleId, CancellationToken cancellationToken) =>
        await dbContext.RolePermissions.Include(rp => rp.Permission).Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);

    public void AddRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Add(rolePermission);

    public void RemoveRolePermission(RolePermission rolePermission) => dbContext.RolePermissions.Remove(rolePermission);

    private IQueryable<RoleRow> QueryRoles(IQueryable<Role> roles) =>
        roles.Select(r => new RoleRow(
            r.Id,
            r.Key,
            r.Name,
            r.Description,
            r.UserType,
            r.IsSystem,
            r.RolePermissions.Select(rp => rp.Permission.Key).ToList(),
            dbContext.UserRoles.Count(ur => ur.RoleId == r.Id)));

    private static RoleDto ToDto(RoleRow row) =>
        new(row.Id, row.Key, row.Name, row.Description, row.UserType.ToString(), row.IsSystem,
            [.. row.Permissions.Order(StringComparer.Ordinal)], row.HolderCount);

    private sealed record RoleRow(
        int Id, string Key, string Name, string? Description, UserType UserType, bool IsSystem, List<string> Permissions, int HolderCount);

    public async Task<IReadOnlyList<UserRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.Role.Name)
            .Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Key, ur.Role.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRole>> ListUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles.Include(ur => ur.Role).Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> FindRolesAsync(IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken) =>
        await dbContext.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);

    public void RemoveUserRole(UserRole userRole) => dbContext.UserRoles.Remove(userRole);
}
