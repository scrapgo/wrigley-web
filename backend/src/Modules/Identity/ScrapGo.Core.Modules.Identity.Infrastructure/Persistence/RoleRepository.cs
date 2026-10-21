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

    public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken) =>
        await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> ListGrantedPermissionKeysAsync(int userId, CancellationToken cancellationToken) =>
        (await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Key))
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlyList<string>> ListPermissionKeysAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);

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
