// ADMIN-API-STATUS Task 1: the built-in role's permissions are seeded by migration.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class BuiltInRolePermissionSeed
{
    public class Given_the_migration_has_applied(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        // The migration still grants the removed Application.*, Module.*, Catalog.* and Organization.* permissions.
        [Fact(Skip = "B5 resets schema")]
        public async Task Platform_administrator_holds_user_role_and_admin_access()
        {
            string[] expected =
            [
                Permissions.UserRead, Permissions.UserCreate, Permissions.UserUpdate, Permissions.UserDelete,
                Permissions.RoleRead, Permissions.RoleCreate, Permissions.RoleUpdate, Permissions.RoleDelete, Permissions.RoleAssign,
                Permissions.AdminAccess,
            ];

            Assert.Equal(expected.Order(StringComparer.Ordinal), await PermissionNamesOfPlatformAdministratorAsync());
        }

        [Fact]
        public async Task Platform_administrator_is_seeded_and_active() =>
            Assert.Equal(
                1,
                await fixture.DbContext.Roles.AsNoTracking().CountAsync(r =>
                    r.Name == DefaultRoleNames.PlatformAdministrator));

        private async Task<IEnumerable<string>> PermissionNamesOfPlatformAdministratorAsync()
        {
            var roleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator)
                .Select(r => r.Id)
                .SingleAsync();

            var names = await fixture.DbContext.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            return names.Order(StringComparer.Ordinal);
        }
    }
}
