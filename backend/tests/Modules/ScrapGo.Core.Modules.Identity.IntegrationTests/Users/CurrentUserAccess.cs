// ADMIN-API-STATUS Task 5: GET /api/users/me reports the caller's effective roles and permissions per scope.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class CurrentUserAccess
{
    public class Given_a_user_with_an_org_role_and_a_platform_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_both_roles_and_each_scopes_permissions_separately()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            var orgRoleId = await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha, SpecPermissions.Beta);
            var platformRoleId = await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserRead);

            var me = await GetMeAsync(fixture, uid);

            Assert.Equal(
                [(platformRoleId, (int?)null), (orgRoleId, organizationId)],
                me.GetProperty("roles").EnumerateArray().Select(r => (r.GetProperty("roleId").GetInt32(), OrganizationIdOf(r))));
            Assert.Equal(
                [((int?)null, Permissions.UserRead), (organizationId, $"{SpecPermissions.Alpha},{SpecPermissions.Beta}")],
                ScopesOf(me));
        }
    }

    public class Given_a_user_with_two_roles_in_the_same_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_the_union_of_their_permissions_in_that_scope()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha);
            await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha, SpecPermissions.Gamma);

            var me = await GetMeAsync(fixture, uid);

            Assert.Equal(2, me.GetProperty("roles").GetArrayLength());
            Assert.Equal(
                [((int?)organizationId, $"{SpecPermissions.Alpha},{SpecPermissions.Gamma}")],
                ScopesOf(me));
        }
    }

    // /me reports what the authorization handler would decide: the same resolver.
    public class Given_the_bootstrapped_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_reports_admin_access_at_platform_scope()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId, organizationId: null);

            var platform = Assert.Single(ScopesOf(await GetMeAsync(fixture, uid)));

            Assert.Null(platform.OrganizationId);
            Assert.Contains(Permissions.AdminAccess, platform.Permissions.Split(','));
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

    // Matches the membership guard: roles in an organization the user no longer
    // belongs to grant nothing there, so they aren't reported as effective.
    public class Given_a_role_in_an_org_whose_membership_is_disabled(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_leaves_that_org_out()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, organizationId, disabled: true);
            await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha);

            var me = await GetMeAsync(fixture, uid);

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
            await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserRead);
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

    private static int? OrganizationIdOf(JsonElement element) =>
        element.GetProperty("organizationId").ValueKind == JsonValueKind.Null ? null : element.GetProperty("organizationId").GetInt32();

    /// <summary>Each scope's permissions joined with commas, in response order, so scopes compare by value.</summary>
    private static List<(int? OrganizationId, string Permissions)> ScopesOf(JsonElement me) =>
        [.. me.GetProperty("permissions").EnumerateArray()
            .Select(s => (OrganizationIdOf(s), string.Join(',', s.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()))))];
}
