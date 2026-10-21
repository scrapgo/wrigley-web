// B15: GET /api/admin/roles (roles.view) and PUT /api/admin/roles/{id}/permissions
// (roles.manage), with the audience rule. Changes go to spec roles, never the
// seeded ones, so other specs sharing a database aren't affected.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class RoleAdministration
{
    public class Given_a_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Listing_roles_shows_the_seeded_roles_and_their_permissions()
        {
            var (uid, _) = await fixture.SeedSiteAdminAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/roles", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            var siteAdmin = Assert.Single(roles, r => r.GetProperty("key").GetString() == DefaultRoleNames.SiteAdmin);
            Assert.Contains(Permissions.RolesManage, siteAdmin.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
            Assert.True(siteAdmin.GetProperty("holderCount").GetInt32() >= 1);
            var user = Assert.Single(roles, r => r.GetProperty("key").GetString() == DefaultRoleNames.User);
            Assert.Equal("External", user.GetProperty("userType").GetString());
            Assert.Equal(
                [Permissions.DashboardView, Permissions.SuppliersViewOwn],
                user.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }

        [Fact]
        public async Task Setting_permissions_replaces_them_audits_and_applies_on_the_holders_next_request()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);
            var (holderUid, holderId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            await fixture.AssignRoleAsync(holderId, roleId);
            Assert.DoesNotContain(Permissions.UsersView, await PermissionsOfAsync(fixture, holderUid));

            var response = await SetAsync(fixture, adminUid, roleId, Permissions.UsersView, Permissions.DashboardView);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var role = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(
                [Permissions.DashboardView, Permissions.UsersView],
                role.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
            Assert.Contains(Permissions.UsersView, await PermissionsOfAsync(fixture, holderUid));
            var audits = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.RolePermissionsChanged && a.UserId == adminId)
                .ToListAsync();
            var audit = JsonDocument.Parse(Assert.Single(audits).Metadata!).RootElement;
            Assert.Equal(roleId, audit.GetProperty("roleId").GetInt32());
            Assert.Equal(
                [Permissions.DashboardView, Permissions.UsersView],
                audit.GetProperty("added").EnumerateArray().Select(k => k.GetString()));
        }

        [Fact]
        public async Task Setting_the_same_permissions_again_writes_nothing()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);
            await SetAsync(fixture, adminUid, roleId, Permissions.DashboardView);

            var response = await SetAsync(fixture, adminUid, roleId, Permissions.DashboardView);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.RolePermissionsChanged && a.UserId == adminId));
        }

        [Fact]
        public async Task An_any_audience_permission_fits_an_external_role()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.External);

            var response = await SetAsync(fixture, adminUid, roleId, Permissions.DashboardView, Permissions.SuppliersViewOwn);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Sad paths, kept separate.

        [Fact]
        public async Task An_external_permission_on_an_internal_role_is_an_audience_mismatch()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);

            var response = await SetAsync(fixture, adminUid, roleId, Permissions.SuppliersViewOwn);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("audience_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.RolePermissions.AnyAsync(rp => rp.RoleId == roleId));
        }

        [Fact]
        public async Task An_internal_permission_on_an_external_role_is_an_audience_mismatch()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.External);

            var response = await SetAsync(fixture, adminUid, roleId, Permissions.UsersView);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("audience_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_key_returns_four_hundred_with_reason_unknown_permission()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);

            var response = await SetAsync(fixture, adminUid, roleId, "invoices.approve");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("unknown_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_role_returns_four_hundred_four()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(fixture, adminUid, 999_999, Permissions.DashboardView)).StatusCode);
        }

        [Fact]
        public async Task A_missing_list_returns_four_hundred_with_reason_invalid_request()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);

            var response = await fixture.SendAsync(HttpMethod.Put, $"/api/admin/roles/{roleId}/permissions", fixture.CreateToken(adminUid), new { });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_scrapgo_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_can_neither_list_roles_nor_change_them()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var scrapGoUserRoleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Key == DefaultRoleNames.ScrapGoUser).Select(r => r.Id).SingleAsync();
            await fixture.AssignRoleAsync(userId, scrapGoUserRoleId);
            var roleId = await CreateRoleAsync(fixture, UserType.Internal);

            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, "/api/admin/roles", fixture.CreateToken(uid))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SetAsync(fixture, uid, roleId, Permissions.DashboardView)).StatusCode);
            Assert.False(await fixture.DbContext.RolePermissions.AnyAsync(rp => rp.RoleId == roleId));
        }
    }

    private static Task<HttpResponseMessage> SetAsync(IdentitySpecFixture fixture, string actorUid, int roleId, params string[] permissions) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/roles/{roleId}/permissions", fixture.CreateToken(actorUid), new { permissions });

    private static async Task<int> CreateRoleAsync(IdentitySpecFixture fixture, UserType userType)
    {
        var role = Role.Create($"spec_role_{Guid.NewGuid():N}", "Spec Role", null, userType, isSystem: false);
        fixture.DbContext.Roles.Add(role);
        await fixture.DbContext.SaveChangesAsync();

        return role.Id;
    }

    private static async Task<List<string>> PermissionsOfAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendMeAsync(fixture.CreateToken(uid));
        response.EnsureSuccessStatusCode();

        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!)];
    }
}
