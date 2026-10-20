// ADMIN-API-STATUS Task 5: GET /api/users/me reports the caller's effective roles and permissions.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class CurrentUserAccess
{
    public class Given_a_user_with_two_roles(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_both_roles_and_the_union_of_their_permissions()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var firstRoleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);
            var secondRoleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha, SpecPermissions.Gamma);

            var me = await GetMeAsync(fixture, uid);

            Assert.Equal(
                new[] { firstRoleId, secondRoleId }.Order(),
                me.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("roleId").GetInt32()).Order());
            Assert.Equal([SpecPermissions.Alpha, SpecPermissions.Gamma], PermissionsOf(me));
        }
    }

    // /me reports what the authorization handler would decide: the same resolver.
    public class Given_the_bootstrapped_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_reports_admin_access()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Key == DefaultRoleNames.SiteAdmin)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId);

            Assert.Contains(Permissions.UsersManage, PermissionsOf(await GetMeAsync(fixture, uid)));
        }
    }

    public class Given_a_brand_new_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_no_roles_and_no_permissions()
        {
            var me = await GetMeAsync(fixture, Guid.NewGuid().ToString());

            Assert.Equal(0, me.GetProperty("roles").GetArrayLength());
            Assert.Equal(0, me.GetProperty("permissions").GetArrayLength());
        }
    }

    // Sad path, kept separate.

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

            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("user_disabled", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    private static async Task<JsonElement> GetMeAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid, email: "me@example.com"));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<string> PermissionsOf(JsonElement me) =>
        [.. me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!)];
}
