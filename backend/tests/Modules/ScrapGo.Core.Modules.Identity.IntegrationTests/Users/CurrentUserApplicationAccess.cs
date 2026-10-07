// ORG-APP-MODULE-MODEL Task 11: /me reports organizations → applications → modules → permissions for UI gating,
// and an org can read back one member's grants.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class CurrentUserApplicationAccess
{
    public class Given_a_member_with_an_app_grant(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_lists_the_org_the_app_its_enabled_module_and_the_permissions()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantApplicationRoleAsync(
                userId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);

            var me = await GetMeAsync(fixture, uid);

            var organization = Assert.Single(me.GetProperty("organizations").EnumerateArray());
            Assert.Equal(organizationId, organization.GetProperty("organizationId").GetInt32());
            var application = Assert.Single(organization.GetProperty("applications").EnumerateArray());
            Assert.Equal(SpecApplications.SpecAppId, application.GetProperty("applicationId").GetInt32());
            Assert.Equal(
                [SpecApplications.AlphaModuleId],
                application.GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("moduleId").GetInt32()));
            Assert.Equal([SpecApplications.AlphaRead], application.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));

            var appScope = Assert.Single(me.GetProperty("permissions").EnumerateArray());
            Assert.Equal(SpecApplications.SpecAppId, appScope.GetProperty("applicationId").GetInt32());
        }

        [Fact]
        public async Task Me_leaves_out_an_app_whose_only_module_is_disabled_and_an_expired_grant()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.BetaModuleId);
            await fixture.EntitleAsync(organizationId, SpecApplications.OtherAppId, SpecApplications.GammaModuleId);
            await fixture.GrantApplicationRoleAsync(
                userId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);
            await fixture.GrantApplicationRoleAsync(
                userId, await fixture.ApplicationRoleIdAsync(SpecApplications.OtherAppAdministrator), organizationId, SpecApplications.OtherAppId,
                expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

            var me = await GetMeAsync(fixture, uid);

            var organization = Assert.Single(me.GetProperty("organizations").EnumerateArray());
            Assert.Equal(0, organization.GetProperty("applications").GetArrayLength());
            // The viewer grant is still held (it just resolves to nothing while Alpha is off); the expired one is gone.
            var role = Assert.Single(me.GetProperty("roles").EnumerateArray());
            Assert.Equal(await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), role.GetProperty("roleId").GetInt32());
        }
    }

    public class Given_an_org_admin_reading_a_members_grants(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Member_grants_lists_org_level_and_application_grants()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            var (_, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, organizationId);
            await fixture.GrantPermissionsAsync(memberId, organizationId, SpecPermissions.Alpha);
            await fixture.GrantApplicationRoleAsync(
                memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"/api/organizations/{organizationId}/members/{memberId}/grants", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var grants = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            Assert.Equal(2, grants.Count);
            Assert.Contains(grants, g => g.GetProperty("applicationId").ValueKind == JsonValueKind.Null);
            Assert.Contains(grants, g => g.GetProperty("applicationId").ValueKind == JsonValueKind.Number);
        }

        [Fact]
        public async Task A_non_member_returns_four_hundred_four()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, outsiderId) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"/api/organizations/{organizationId}/members/{outsiderId}/grants", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private static async Task<JsonElement> GetMeAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid, email: "me@example.com"));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
