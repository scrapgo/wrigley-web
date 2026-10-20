// B10: GET /api/me reports the caller's user, roles, effective permissions and features.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class CurrentUserAccess
{
    public class Given_a_user_with_two_roles(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_both_role_keys_and_the_union_of_their_permissions()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var firstRoleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);
            var secondRoleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha, SpecPermissions.Gamma);
            var roleKeys = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Id == firstRoleId || r.Id == secondRoleId).Select(r => r.Key).ToListAsync();

            var me = await GetMeAsync(fixture, uid);

            Assert.Equal(roleKeys.Order(), StringsOf(me, "roles").Order());
            Assert.Equal([SpecPermissions.Alpha, SpecPermissions.Gamma], StringsOf(me, "permissions"));
        }
    }

    // /me reports what the authorization handler would decide: the same access context.
    public class Given_a_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_reports_admin_access_and_the_locked_features()
        {
            var (uid, userId) = await fixture.SeedUserAsync("admin@scrapgo.example", UserType.Internal);
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Key == DefaultRoleNames.SiteAdmin)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId);

            var me = await GetMeAsync(fixture, uid);

            Assert.Contains(DefaultRoleNames.SiteAdmin, StringsOf(me, "roles"));
            Assert.Contains(Permissions.UsersManage, StringsOf(me, "permissions"));
            Assert.Contains(FeatureKeys.Users, StringsOf(me, "features"));
            var user = me.GetProperty("user");
            Assert.Equal(userId, user.GetProperty("id").GetInt32());
            Assert.Equal("admin@scrapgo.example", user.GetProperty("email").GetString());
            Assert.Equal("Internal", user.GetProperty("userType").GetString());
            Assert.Equal("Active", user.GetProperty("status").GetString());
        }
    }

    public class Given_a_user_with_no_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_no_roles_and_no_permissions()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var me = await GetMeAsync(fixture, uid);

            Assert.Empty(StringsOf(me, "roles"));
            Assert.Empty(StringsOf(me, "permissions"));
        }
    }

    // Sad paths, kept separate.

    public class Given_an_unknown_uid(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_four_hundred_three_no_access()
        {
            var response = await fixture.SendMeAsync(fixture.CreateToken(Guid.NewGuid().ToString(), email: "stranger@example.com"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("no_access", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_disabled_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_is_blocked_by_the_disabled_user_gate()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, Permissions.UsersView);
            var user = await fixture.DbContext.Users.SingleAsync(u => u.Id == userId);
            user.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();
            fixture.EvictAccess();

            var response = await fixture.SendMeAsync(fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("account_disabled", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    private static async Task<JsonElement> GetMeAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendMeAsync(fixture.CreateToken(uid, email: "me@example.com"));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<string> StringsOf(JsonElement me, string property) =>
        [.. me.GetProperty(property).EnumerateArray().Select(p => p.GetString()!)];
}
