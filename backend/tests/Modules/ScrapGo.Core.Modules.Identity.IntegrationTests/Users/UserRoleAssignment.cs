// B14: PUT /api/admin/users/{id}/roles and /suppliers (users.manage) replace the whole set.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class UserRoleAssignment
{
    public class Given_a_site_admin_and_an_internal_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Setting_roles_returns_the_user_writes_the_rows_and_one_audit_entry()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var scrapGoUserId = await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser);

            var response = await SetRolesAsync(fixture, adminUid, userId, scrapGoUserId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([DefaultRoleNames.ScrapGoUser], body.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("key").GetString()));
            Assert.Equal([DefaultRoleNames.ScrapGoUser], await RoleKeysOfAsync(fixture, userId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.UserRolesChanged, userId));
            Assert.Equal(adminId, audit.UserId);
        }

        [Fact]
        public async Task Setting_the_same_roles_again_writes_nothing()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var scrapGoUserId = await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser);
            await SetRolesAsync(fixture, adminUid, userId, scrapGoUserId);

            var response = await SetRolesAsync(fixture, adminUid, userId, scrapGoUserId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.UserRolesChanged, userId));
        }

        [Fact]
        public async Task An_empty_list_removes_every_role()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            await SetRolesAsync(fixture, adminUid, userId, await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser));

            var response = await SetRolesAsync(fixture, adminUid, userId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await RoleKeysOfAsync(fixture, userId));
        }

        [Fact]
        public async Task The_change_takes_effect_on_the_users_next_request()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(uid))).StatusCode);

            await SetRolesAsync(fixture, adminUid, userId, await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin));

            Assert.Equal(HttpStatusCode.OK, (await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(uid))).StatusCode);
        }

        [Fact]
        public async Task Removing_site_admin_from_one_of_two_active_holders_is_allowed()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, secondId) = await fixture.SeedSiteAdminAsync();

            var response = await SetRolesAsync(fixture, adminUid, secondId, await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([DefaultRoleNames.ScrapGoUser], await RoleKeysOfAsync(fixture, secondId));
        }

        // Sad paths, kept separate.

        [Fact]
        public async Task An_external_role_for_an_internal_user_is_a_type_mismatch()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

            var response = await SetRolesAsync(fixture, adminUid, userId, await RoleIdAsync(fixture, DefaultRoleNames.User));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("role_type_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Empty(await RoleKeysOfAsync(fixture, userId));
        }

        [Fact]
        public async Task An_unknown_role_returns_four_hundred_with_reason_role_not_found()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

            var response = await SetRolesAsync(fixture, adminUid, userId, 999_999);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("role_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_missing_role_list_returns_four_hundred_with_reason_invalid_request()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Put, $"/api/admin/users/{userId}/roles", fixture.CreateToken(adminUid), new { });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_user_returns_four_hundred_four()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await SetRolesAsync(fixture, adminUid, 999_999)).StatusCode);
        }
    }

    // One database per class below: the rule is about every Site Admin there is.

    public class Given_the_only_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Removing_their_site_admin_role_returns_four_hundred_nine()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();

            var response = await SetRolesAsync(fixture, adminUid, adminId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_site_admin", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal([DefaultRoleNames.SiteAdmin], await RoleKeysOfAsync(fixture, adminId));
        }
    }

    public class Given_the_only_active_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Removing_is_still_blocked_when_the_only_other_holder_is_disabled()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var (_, disabledId) = await fixture.SeedSiteAdminAsync();
            (await fixture.SendAsync(HttpMethod.Post, $"/api/admin/users/{disabledId}/disable", fixture.CreateToken(adminUid))).EnsureSuccessStatusCode();

            var response = await SetRolesAsync(fixture, adminUid, adminId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    public class Given_an_external_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Setting_suppliers_replaces_the_links_and_scopes_their_next_request()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
            (await SetSuppliersAsync(fixture, adminUid, userId, 17511, 9583)).EnsureSuccessStatusCode();

            var response = await SetSuppliersAsync(fixture, adminUid, userId, 9583, 4242);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([4242, 9583], body.GetProperty("supplierRecordIds").EnumerateArray().Select(i => i.GetInt32()));
            Assert.Equal(2, (await AuditAsync(fixture, IdentityAuditEventTypes.UserSuppliersChanged, userId)).Count);
            Assert.All(await AuditAsync(fixture, IdentityAuditEventTypes.UserSuppliersChanged, userId), a => Assert.Equal(adminId, a.UserId));
        }

        [Fact]
        public async Task An_external_role_can_be_given()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");

            var response = await SetRolesAsync(fixture, adminUid, userId, await RoleIdAsync(fixture, DefaultRoleNames.User));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([DefaultRoleNames.User], await RoleKeysOfAsync(fixture, userId));
        }
    }

    public class Given_an_internal_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Setting_suppliers_returns_four_hundred_with_reason_supplier_links_external_only()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

            var response = await SetSuppliersAsync(fixture, adminUid, userId, 17511);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("supplier_links_external_only", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.UserSuppliers.AnyAsync(s => s.UserId == userId));
        }
    }

    public class Given_a_user_holding_users_view_but_not_users_manage(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Setting_roles_and_suppliers_returns_four_hundred_three()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, Permissions.UsersView);
            var (_, targetId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");

            Assert.Equal(HttpStatusCode.Forbidden, (await SetRolesAsync(fixture, uid, targetId)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SetSuppliersAsync(fixture, uid, targetId, 17511)).StatusCode);
            Assert.False(await fixture.DbContext.UserSuppliers.AnyAsync(s => s.UserId == targetId));
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("roles")]
        [InlineData("suppliers")]
        public async Task It_returns_four_hundred_one(string resource)
        {
            var response = await fixture.Client.PutAsJsonAsync($"/api/admin/users/1/{resource}", new { });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> SetRolesAsync(IdentitySpecFixture fixture, string actorUid, int userId, params int[] roleIds) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/users/{userId}/roles", fixture.CreateToken(actorUid), new { roleIds });

    private static Task<HttpResponseMessage> SetSuppliersAsync(
        IdentitySpecFixture fixture, string actorUid, int userId, params int[] supplierRecordIds) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/users/{userId}/suppliers", fixture.CreateToken(actorUid), new { supplierRecordIds });

    private static Task<int> RoleIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == key).Select(r => r.Id).SingleAsync();

    private static Task<List<string>> RoleKeysOfAsync(IdentitySpecFixture fixture, int userId) =>
        fixture.DbContext.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.Role.Key).ToListAsync();

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
