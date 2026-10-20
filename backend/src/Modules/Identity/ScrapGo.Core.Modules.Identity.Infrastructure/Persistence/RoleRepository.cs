using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

/// <remarks>
/// Every query skips rows with a <see cref="LegacyScope"/>: organization-scoped
/// roles and grants the old schema still holds until B6.
/// </remarks>
public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    private IQueryable<Role> Roles => dbContext.Roles.Where(r => EF.Property<int?>(r, LegacyScope.Property) == null);

    private IQueryable<UserRole> UserRoles => dbContext.UserRoles.Where(ur => EF.Property<int?>(ur, LegacyScope.Property) == null);

    public Task<Role?> FindActiveRoleAsync(int roleId, CancellationToken cancellationToken) =>
        Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == roleId && r.Status == RoleStatus.Active, cancellationToken);

    public Task<int> GetPlatformRoleIdAsync(string roleName, CancellationToken cancellationToken) =>
        Roles
            .AsNoTracking()
            .Where(r => r.Name == roleName)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

    public Task LockRoleAsync(int roleId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.roles WHERE id = {roleId} FOR UPDATE", cancellationToken);

    public Task<int> CountActiveHoldersAsync(int roleId, int excludingUserId, CancellationToken cancellationToken) =>
        (from ur in UserRoles.AsNoTracking()
         join u in dbContext.Users.AsNoTracking() on ur.UserId equals u.Id
         where ur.RoleId == roleId
             && ur.UserId != excludingUserId
             && u.Status == UserStatus.Active
         select ur.UserId)
        .Distinct()
        .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> ListHolderIdsAsync(int roleId, CancellationToken cancellationToken) =>
        await UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<bool> IsHeldByAsync(int userId, int roleId, CancellationToken cancellationToken) =>
        UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

    public void AddUserRole(UserRole userRole) => dbContext.UserRoles.Add(userRole);

    public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken) =>
        await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssignedRoleDto>> ListUserRolesAsync(int userId, CancellationToken cancellationToken) =>
        await UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.Status == RoleStatus.Active)
            .OrderBy(ur => ur.Role.Name)
            .Select(ur => new AssignedRoleDto(ur.RoleId, ur.Role.Name, ur.ExpiresAt))
            .ToListAsync(cancellationToken);

    public Task<UserRole?> FindUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken) =>
        UserRoles.SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

    public void RemoveUserRole(UserRole userRole) => dbContext.UserRoles.Remove(userRole);
}
