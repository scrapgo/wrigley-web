// Platform access requires a Google Workspace sign-in (hd on INTERNAL_HD_ALLOWLIST) on every request,
// not just an Internal classification at provisioning (ORG-APP-MODULE-MODEL.md section 8.1).
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class WorkspaceSignInForPlatformAccess
{
    private const string PlatformProbe = "/api/_test/platform-scoped";

    public class Given_a_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_workspace_sign_in_reaches_platform_routes()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformProbe, fixture.CreateToken(uid, hd: IdentitySpecFixture.InternalDomain));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // E.g. the same person signing in through a linked email/password account.
        [Fact]
        public async Task A_sign_in_without_hd_gets_four_hundred_three_with_reason_workspace_sign_in_required()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformProbe, fixture.CreateToken(uid, withoutHostedDomain: true));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("workspace_sign_in_required", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_hosted_domain_not_on_the_allow_list_is_refused_too()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformProbe, fixture.CreateToken(uid, hd: "some-other-workspace.example"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("workspace_sign_in_required", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // /me reports what the server would decide: no platform scope without Workspace.
        [Fact]
        public async Task Me_without_hd_reports_no_platform_roles_or_permissions()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var withWorkspace = await GetMeAsync(fixture, fixture.CreateToken(uid));
            var withoutWorkspace = await GetMeAsync(fixture, fixture.CreateToken(uid, withoutHostedDomain: true));

            Assert.Contains(withWorkspace.GetProperty("permissions").EnumerateArray(), s => s.GetProperty("organizationId").ValueKind == JsonValueKind.Null);
            Assert.Equal(0, withoutWorkspace.GetProperty("permissions").GetArrayLength());
            Assert.Equal(0, withoutWorkspace.GetProperty("roles").GetArrayLength());

            // ...but says why, so the portal can prompt for a Google sign-in.
            Assert.True(withoutWorkspace.GetProperty("workspaceSignInRequired").GetBoolean());
            Assert.False(withWorkspace.GetProperty("workspaceSignInRequired").GetBoolean());
        }

        // In-service platform checks (no [RequirePermission]) use the same resolver, so the same rule.
        [Fact]
        public async Task An_in_service_platform_action_without_hd_is_refused()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, targetId) = await fixture.SeedUserAsync("staff@scrapgo.example", UserClassification.Internal);
            var platformAdminRoleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Post, $"/api/users/{targetId}/roles", fixture.CreateToken(uid, withoutHostedDomain: true), new { roleId = platformAdminRoleId });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == targetId));
        }
    }

    // Only platform scope is affected: organization access works from any sign-in method.
    public class Given_an_org_admin_signing_in_without_workspace(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Organization_routes_still_work()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"/api/organizations/{organizationId}", fixture.CreateToken(uid, withoutHostedDomain: true));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // No platform role, so nothing to prompt for.
        [Fact]
        public async Task Me_does_not_ask_for_a_workspace_sign_in()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var me = await GetMeAsync(fixture, fixture.CreateToken(uid, withoutHostedDomain: true));

            Assert.False(me.GetProperty("workspaceSignInRequired").GetBoolean());
        }
    }

    private static async Task<JsonElement> GetMeAsync(IdentitySpecFixture fixture, string token)
    {
        var response = await fixture.SendUsersMeAsync(token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
