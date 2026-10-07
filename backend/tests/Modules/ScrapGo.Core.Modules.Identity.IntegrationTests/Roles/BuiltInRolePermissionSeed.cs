// ADMIN-API-STATUS Task 1: the built-in roles' permissions are seeded by migration.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class BuiltInRolePermissionSeed
{
    public class Given_the_migration_has_applied(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Platform_administrator_holds_user_role_admin_access_and_platform_administration()
        {
            string[] expected =
            [
                Permissions.UserRead, Permissions.UserCreate, Permissions.UserUpdate, Permissions.UserDelete,
                Permissions.RoleRead, Permissions.RoleCreate, Permissions.RoleUpdate, Permissions.RoleDelete, Permissions.RoleAssign,
                Permissions.AdminAccess,
                Permissions.OrganizationCreate, Permissions.OrganizationDeactivate,
                Permissions.ApplicationAssign, Permissions.ModuleManage, Permissions.CatalogManage,
            ];

            Assert.Equal(expected.Order(StringComparer.Ordinal), await PermissionNamesOfAsync(DefaultRoleNames.PlatformAdministrator));
        }

        [Fact]
        public async Task Organization_administrator_holds_every_permission_except_admin_access() =>
            Assert.Equal(
                SpecPermissions.OrganizationAdministratorGrants.Order(StringComparer.Ordinal),
                await PermissionNamesOfAsync(DefaultRoleNames.OrganizationAdministrator));

        [Fact]
        public async Task Both_built_in_roles_are_platform_defined_and_active() =>
            Assert.Equal(
                2,
                await fixture.DbContext.Roles.AsNoTracking().CountAsync(r =>
                    (r.Name == DefaultRoleNames.PlatformAdministrator || r.Name == DefaultRoleNames.OrganizationAdministrator)
                    && r.OrganizationId == null
                    && r.Status == RoleStatus.Active));

        private async Task<IEnumerable<string>> PermissionNamesOfAsync(string builtInRoleName)
        {
            var roleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Name == builtInRoleName && r.OrganizationId == null)
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
