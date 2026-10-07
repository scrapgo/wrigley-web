// STORY-017: Compose a role from a subset of the permission catalog.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class ComposeRoleFromPermissionSubset
{
    public class Given_an_existing_role_and_the_seeded_catalog(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_role_permissions_attaches_a_permission_by_name()
        {
            var (uid, roleId) = await SeedAdminWithRoleAsync(fixture);

            var response = await AttachAsync(fixture, uid, roleId, SpecPermissions.Alpha);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([SpecPermissions.Alpha], await PermissionNamesOfAsync(fixture, roleId));
        }

        [Fact]
        public async Task Attaching_the_same_permission_twice_is_idempotent()
        {
            var (uid, roleId) = await SeedAdminWithRoleAsync(fixture);
            (await AttachAsync(fixture, uid, roleId, SpecPermissions.Alpha)).EnsureSuccessStatusCode();

            var response = await AttachAsync(fixture, uid, roleId, SpecPermissions.Alpha);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([SpecPermissions.Alpha], await PermissionNamesOfAsync(fixture, roleId));
        }

        [Fact]
        public async Task Delete_role_permissions_detaches_only_the_named_permission()
        {
            var (uid, roleId) = await SeedAdminWithRoleAsync(fixture);
            (await AttachAsync(fixture, uid, roleId, SpecPermissions.Alpha)).EnsureSuccessStatusCode();
            (await AttachAsync(fixture, uid, roleId, SpecPermissions.Beta)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(
                HttpMethod.Delete, $"/api/roles/{roleId}/permissions/{SpecPermissions.Alpha}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal([SpecPermissions.Beta], await PermissionNamesOfAsync(fixture, roleId));
        }
    }

    // Sad paths, kept separate.

    public class Given_an_unknown_permission_name(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_role_permissions_returns_four_hundred_with_reason_unknown_permission()
        {
            var (uid, roleId) = await SeedAdminWithRoleAsync(fixture);

            var response = await AttachAsync(fixture, uid, roleId, "Invoice.Launder");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("unknown_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_an_admin_of_a_different_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Attaching_to_another_orgs_role_returns_four_hundred_three()
        {
            var (_, roleId) = await SeedAdminWithRoleAsync(fixture);
            var (otherAdminUid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await AttachAsync(fixture, otherAdminUid, roleId, Permissions.AdminAccess);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await PermissionNamesOfAsync(fixture, roleId));
        }
    }

    // Escalation guard: an organization administrator holds everything but
    // Admin.Access, so it can't put Admin.Access on a role it hands out.
    public class Given_an_org_admin_attaching_a_permission_they_do_not_hold(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_role_permissions_returns_four_hundred_three_with_reason_cannot_grant_unheld_permission()
        {
            var (uid, roleId) = await SeedAdminWithRoleAsync(fixture);

            var response = await AttachAsync(fixture, uid, roleId, Permissions.AdminAccess);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_grant_unheld_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Empty(await PermissionNamesOfAsync(fixture, roleId));
        }
    }

    // Escalation guard: a Role.Update holder can't widen the very role that
    // gives them Role.Update.
    public class Given_a_member_composing_a_role_they_hold(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_role_permissions_returns_four_hundred_three_with_reason_cannot_modify_own_role()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            var ownRoleId = await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.RoleUpdate, SpecPermissions.Alpha);

            var response = await AttachAsync(fixture, uid, ownRoleId, SpecPermissions.Alpha);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_modify_own_role", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_role_id_that_does_not_exist(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_role_permissions_returns_four_hundred_four()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await AttachAsync(fixture, uid, roleId: 999_999, SpecPermissions.Alpha);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private static async Task<(string Uid, int RoleId)> SeedAdminWithRoleAsync(IdentitySpecFixture fixture)
    {
        var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
        var roleId = await CustomRoleCrud.ReadIdAsync(
            await CustomRoleCrud.PostRoleAsync(fixture, uid, organizationId, "Composable Role", null));

        return (uid, roleId);
    }

    private static Task<HttpResponseMessage> AttachAsync(IdentitySpecFixture fixture, string uid, int roleId, string permissionName) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/roles/{roleId}/permissions", fixture.CreateToken(uid), new { permissionName });

    private static Task<List<string>> PermissionNamesOfAsync(IdentitySpecFixture fixture, int roleId) =>
        fixture.DbContext.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Name)
            .OrderBy(name => name)
            .ToListAsync();
}
