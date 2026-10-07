// ORG-APP-MODULE-MODEL Task 6: platform admins assign applications to organizations and enable licensed modules.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Applications;

public class EntitlementSpecs
{
    public class Given_a_platform_administrator_and_an_organization(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_is_idempotent_audited_and_grants_nobody_anything()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, memberId, organizationId) = await fixture.SeedMemberAsync();

            var first = await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId);
            var second = await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId);

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.ApplicationAssigned && a.OrganizationId == organizationId).ToListAsync());
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == memberId));
        }

        [Fact]
        public async Task Enabling_a_module_requires_the_application_to_be_assigned()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await EnableModuleAsync(fixture, uid, organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application_not_assigned", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // A module of another application can never be enabled here.
        [Fact]
        public async Task Enabling_another_applications_module_returns_four_hundred_four()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            (await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId)).EnsureSuccessStatusCode();

            var response = await EnableModuleAsync(fixture, uid, organizationId, SpecApplications.SpecAppId, SpecApplications.GammaModuleId);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("module_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Get_organization_applications_lists_assigned_apps_with_enabled_modules_only()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            (await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId)).EnsureSuccessStatusCode();
            (await EnableModuleAsync(fixture, uid, organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/organizations/{organizationId}/applications", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var application = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
            Assert.Equal(SpecApplications.SpecAppId, application.GetProperty("applicationId").GetInt32());
            Assert.Equal(
                [SpecApplications.AlphaModuleId],
                application.GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("moduleId").GetInt32()));
        }

        // The organization route needs membership; platform admins read through /api/admin.
        [Fact]
        public async Task A_platform_admin_who_is_not_a_member_reads_them_through_the_admin_route()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            (await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/organizations/{organizationId}/applications", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var application = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
            Assert.Equal(SpecApplications.SpecAppId, application.GetProperty("applicationId").GetInt32());
        }

        [Fact]
        public async Task The_admin_route_refuses_an_organization_administrator()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/organizations/{organizationId}/applications", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Decision 5: removing an application hard-revokes every grant for it in the organization.
    public class Given_an_application_with_grants_being_removed(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Every_grant_is_revoked_and_audited_and_reassigning_restores_no_access()
        {
            var (uid, platformAdminId) = await fixture.SeedPlatformAdministratorAsync();
            var (memberUid, memberId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantApplicationRoleAsync(
                memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);
            Assert.Equal(HttpStatusCode.OK, (await AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);

            var response = await fixture.SendAsync(
                HttpMethod.Delete, $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.OrganizationId == organizationId && ur.ApplicationId == SpecApplications.SpecAppId));
            var revoked = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.AccessRevoked && a.OrganizationId == organizationId).ToListAsync());
            Assert.Equal(platformAdminId, revoked.UserId);
            var removed = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.ApplicationRemoved && a.OrganizationId == organizationId).ToListAsync());
            using (var metadata = JsonDocument.Parse(removed.Metadata!))
            {
                Assert.Equal(1, metadata.RootElement.GetProperty("revokedGrantCount").GetInt32());
            }

            Assert.Equal(HttpStatusCode.NotFound, (await AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);

            (await AssignAsync(fixture, uid, organizationId, SpecApplications.SpecAppId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);
        }
    }

    // Sad path, kept separate.

    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("PUT", "")]
        [InlineData("DELETE", "")]
        [InlineData("PUT", "/modules/901")]
        [InlineData("DELETE", "/modules/901")]
        public async Task Every_entitlement_route_returns_four_hundred_three_even_for_their_own_org(string method, string suffix)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                new HttpMethod(method),
                $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}{suffix}",
                fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await fixture.DbContext.OrganizationApplications.AsNoTracking().AnyAsync(oa => oa.OrganizationId == organizationId));
        }
    }

    internal static Task<HttpResponseMessage> AssignAsync(IdentitySpecFixture fixture, string uid, int organizationId, int applicationId) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/admin/organizations/{organizationId}/applications/{applicationId}", fixture.CreateToken(uid));

    internal static Task<HttpResponseMessage> EnableModuleAsync(
        IdentitySpecFixture fixture, string uid, int organizationId, int applicationId, int moduleId) =>
        fixture.SendAsync(
            HttpMethod.Put, $"/api/admin/organizations/{organizationId}/applications/{applicationId}/modules/{moduleId}", fixture.CreateToken(uid));

    internal static Task<HttpResponseMessage> AppProbeAsync(IdentitySpecFixture fixture, string uid, int organizationId) =>
        fixture.SendAsync(
            HttpMethod.Get, $"/api/_test/app-scoped/{organizationId}/{SpecApplications.SpecAppId}", fixture.CreateToken(uid));
}
