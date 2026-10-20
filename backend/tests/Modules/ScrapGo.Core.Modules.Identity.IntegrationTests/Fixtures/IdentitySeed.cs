namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Arranges identity/RBAC rows directly through the real DbContext, using the
/// domain factories, so scenarios read as intent ("a platform admin", "a user
/// holding Spec.Alpha") rather than row plumbing.
/// </summary>
public static class IdentitySeed
{
    /// <param name="classification">External by default, as for any sign-in outside the internal Workspace domains.</param>
    public static async Task<(string Uid, int UserId)> SeedUserAsync(
        this IdentitySpecFixture fixture,
        string email = "member@example.com",
        UserClassification classification = UserClassification.External)
    {
        var uid = Guid.NewGuid().ToString();
        var user = User.Provision(uid, email, classification, DateTimeOffset.UtcNow);
        fixture.DbContext.Users.Add(user);
        await fixture.DbContext.SaveChangesAsync();
        if (classification == UserClassification.Internal)
        {
            fixture.MarkInternal(uid);
        }

        return (uid, user.Id);
    }

    /// <summary>A new internal user holding the built-in PlatformAdministrator.</summary>
    public static async Task<(string Uid, int UserId)> SeedPlatformAdministratorAsync(this IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync("platform-admin@scrapgo.example", UserClassification.Internal);
        var roleId = await fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator)
            .Select(r => r.Id)
            .SingleAsync();
        await fixture.AssignRoleAsync(userId, roleId);

        return (uid, userId);
    }

    /// <summary>Creates a role granting <paramref name="permissionNames"/> and assigns it to the user.</summary>
    public static async Task<int> GrantPermissionsAsync(this IdentitySpecFixture fixture, int userId, params string[] permissionNames)
    {
        var role = Role.Create($"Spec Role {Guid.NewGuid():N}", null, DateTimeOffset.UtcNow);

        fixture.DbContext.Roles.Add(role);
        await fixture.DbContext.SaveChangesAsync();

        var permissionIds = await fixture.DbContext.Permissions
            .Where(p => permissionNames.Contains(p.Name))
            .Select(p => p.Id)
            .ToListAsync();

        fixture.DbContext.RolePermissions.AddRange(permissionIds.Select(permissionId => RolePermission.Create(role.Id, permissionId)));
        await fixture.DbContext.SaveChangesAsync();

        await fixture.AssignRoleAsync(userId, role.Id);

        return role.Id;
    }

    /// <summary>
    /// Grants need a Workspace sign-in to resolve, which only internal users
    /// have, so assigning a role makes the holder Internal, and their tokens
    /// then carry the internal <c>hd</c>.
    /// </summary>
    public static async Task AssignRoleAsync(this IdentitySpecFixture fixture, int userId, int roleId)
    {
        await fixture.MakeInternalAsync(userId);

        fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, roleId, DateTimeOffset.UtcNow));
        await fixture.DbContext.SaveChangesAsync();
    }

    private static async Task MakeInternalAsync(this IdentitySpecFixture fixture, int userId)
    {
        await fixture.DbContext.Database.ExecuteSqlAsync(
            $"UPDATE identity.users SET classification = 'Internal' WHERE id = {userId}");
        var uid = await fixture.DbContext.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.IdentityPlatformUid).SingleAsync();
        fixture.MarkInternal(uid);
    }
}
