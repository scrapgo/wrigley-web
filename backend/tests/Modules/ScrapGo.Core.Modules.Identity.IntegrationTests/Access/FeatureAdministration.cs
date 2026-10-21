// B16: GET /api/admin/features (features.view), PUT …/{id} and …/{id}/targets
// (features.manage). Each class gets its own database, since these change the
// seeded features.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Access;

public class FeatureAdministration
{
    // supplier_edit is seeded Targeted with no targets, so scrapgo_user's
    // suppliers.edit_all reaches nobody until someone is targeted.
    public class Given_supplier_edit_is_targeted_at_one_scrapgo_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Only_that_users_me_gains_the_feature_and_its_permission()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var (targetUid, targetId) = await SeedScrapGoUserAsync(fixture);
            var (otherUid, _) = await SeedScrapGoUserAsync(fixture);
            var supplierEdit = await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit);
            Assert.DoesNotContain(Permissions.SuppliersEditAll, (await MeAsync(fixture, targetUid)).Permissions);

            var state = await SetStateAsync(fixture, adminUid, supplierEdit, "Targeted");
            var targets = await SetTargetsAsync(fixture, adminUid, supplierEdit, userIds: [targetId], roleIds: []);

            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            Assert.Equal(HttpStatusCode.OK, targets.StatusCode);
            var feature = await targets.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([targetId], feature.GetProperty("targets").GetProperty("userIds").EnumerateArray().Select(i => i.GetInt32()));

            var target = await MeAsync(fixture, targetUid);
            Assert.Contains(Permissions.SuppliersEditAll, target.Permissions);
            Assert.Contains(FeatureKeys.SupplierEdit, target.Features);
            var other = await MeAsync(fixture, otherUid);
            Assert.DoesNotContain(Permissions.SuppliersEditAll, other.Permissions);
            Assert.DoesNotContain(FeatureKeys.SupplierEdit, other.Features);

            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.FeatureTargetsChanged && a.UserId == adminId));
        }
    }

    public class Given_supplier_edit_is_targeted_at_a_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Every_holder_of_the_role_gains_it()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var (firstUid, _) = await SeedScrapGoUserAsync(fixture);
            var (secondUid, _) = await SeedScrapGoUserAsync(fixture);
            var scrapGoUser = await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser);

            await SetTargetsAsync(fixture, adminUid, await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), userIds: [], roleIds: [scrapGoUser]);

            Assert.Contains(Permissions.SuppliersEditAll, (await MeAsync(fixture, firstUid)).Permissions);
            Assert.Contains(Permissions.SuppliersEditAll, (await MeAsync(fixture, secondUid)).Permissions);
        }
    }

    public class Given_a_toggleable_feature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Switching_it_on_then_off_changes_every_holders_access_and_audits_each_change()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();
            var (uid, _) = await SeedScrapGoUserAsync(fixture);
            var supplierEdit = await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit);

            var on = await SetStateAsync(fixture, adminUid, supplierEdit, "on");
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
            Assert.Equal("On", (await on.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
            Assert.Contains(Permissions.SuppliersEditAll, (await MeAsync(fixture, uid)).Permissions);

            await SetStateAsync(fixture, adminUid, supplierEdit, "Off");
            Assert.DoesNotContain(Permissions.SuppliersEditAll, (await MeAsync(fixture, uid)).Permissions);

            Assert.Equal(2, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.FeatureStateChanged && a.UserId == adminId));
        }

        [Fact]
        public async Task Listing_shows_each_feature_with_its_permissions_and_lock()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/features", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var features = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            var users = Assert.Single(features, f => f.GetProperty("key").GetString() == FeatureKeys.Users);
            Assert.False(users.GetProperty("isToggleable").GetBoolean());
            Assert.Contains(Permissions.UsersManage, users.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
            var supplierEdit = Assert.Single(features, f => f.GetProperty("key").GetString() == FeatureKeys.SupplierEdit);
            Assert.True(supplierEdit.GetProperty("isToggleable").GetBoolean());
        }

        // Sad paths, kept separate.

        [Theory]
        [InlineData("Maybe")]
        [InlineData("1")]
        [InlineData("")]
        public async Task An_unknown_state_returns_four_hundred(string state)
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            var response = await SetStateAsync(fixture, adminUid, await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), state);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_target_user_returns_four_hundred_with_reason_user_not_found()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            var response = await SetTargetsAsync(
                fixture, adminUid, await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), userIds: [999_999], roleIds: []);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("user_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_unknown_feature_returns_four_hundred_four()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await SetStateAsync(fixture, adminUid, 999_999, "On")).StatusCode);
        }
    }

    public class Given_a_locked_feature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Changing_its_state_returns_four_hundred_nine_feature_locked_and_changes_nothing()
        {
            var (adminUid, _) = await fixture.SeedSiteAdminAsync();
            var usersFeature = await FeatureIdAsync(fixture, FeatureKeys.Users);

            var response = await SetStateAsync(fixture, adminUid, usersFeature, "Off");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("feature_locked", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(FeatureState.On, await fixture.DbContext.Features.AsNoTracking()
                .Where(f => f.Id == usersFeature).Select(f => f.State).SingleAsync());
        }

        [Fact]
        public async Task Targeting_it_returns_four_hundred_nine_feature_locked()
        {
            var (adminUid, adminId) = await fixture.SeedSiteAdminAsync();

            var response = await SetTargetsAsync(fixture, adminUid, await FeatureIdAsync(fixture, FeatureKeys.Users), userIds: [adminId], roleIds: []);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("feature_locked", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_scrapgo_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_can_neither_list_nor_change_features()
        {
            var (uid, _) = await SeedScrapGoUserAsync(fixture);
            var supplierEdit = await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit);

            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, "/api/admin/features", fixture.CreateToken(uid))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SetStateAsync(fixture, uid, supplierEdit, "On")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SetTargetsAsync(fixture, uid, supplierEdit, userIds: [], roleIds: [])).StatusCode);
            Assert.Equal(FeatureState.Targeted, await fixture.DbContext.Features.AsNoTracking()
                .Where(f => f.Id == supplierEdit).Select(f => f.State).SingleAsync());
        }
    }

    private sealed record Me(IReadOnlyList<string> Permissions, IReadOnlyList<string> Features);

    private static async Task<Me> MeAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendMeAsync(fixture.CreateToken(uid));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new(
            [.. body.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!)],
            [.. body.GetProperty("features").EnumerateArray().Select(f => f.GetString()!)]);
    }

    private static Task<HttpResponseMessage> SetStateAsync(IdentitySpecFixture fixture, string actorUid, int featureId, string state) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/features/{featureId}", fixture.CreateToken(actorUid), new { state });

    private static Task<HttpResponseMessage> SetTargetsAsync(
        IdentitySpecFixture fixture, string actorUid, int featureId, int[] userIds, int[] roleIds) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/features/{featureId}/targets", fixture.CreateToken(actorUid), new { userIds, roleIds });

    private static async Task<(string Uid, int UserId)> SeedScrapGoUserAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
        await fixture.AssignRoleAsync(userId, await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser));

        return (uid, userId);
    }

    private static Task<int> RoleIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == key).Select(r => r.Id).SingleAsync();

    private static Task<int> FeatureIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Features.AsNoTracking().Where(f => f.Key == key).Select(f => f.Id).SingleAsync();
}
