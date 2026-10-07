// ORG-APP-MODULE-MODEL Tasks 7 and 8: permissions resolve per (organization, application), only for
// enabled modules, only while the grant is unexpired; the route's {applicationId} selects the scope.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Applications;

public class ApplicationScopedResolution
{
    public class Given_a_member_granted_spec_app_viewer(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_app_scoped_check_passes_while_the_module_is_enabled()
        {
            var (uid, _, organizationId) = await SeedViewerAsync(fixture, SpecApplications.AlphaModuleId);

            Assert.Equal(HttpStatusCode.OK, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        // Module-disabled ⇒ deny, on the very next request, whatever the role grants; re-enabling restores it.
        [Fact]
        public async Task Disabling_the_module_denies_the_next_request_and_re_enabling_restores_it()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (uid, userId, organizationId) = await SeedViewerAsync(fixture, SpecApplications.AlphaModuleId);
            var modulePath = $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}/modules/{SpecApplications.AlphaModuleId}";

            (await fixture.SendAsync(HttpMethod.Delete, modulePath, fixture.CreateToken(platformUid))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
            Assert.True(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.ApplicationId != null));

            (await fixture.SendAsync(HttpMethod.Put, modulePath, fixture.CreateToken(platformUid))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task A_module_never_enabled_denies()
        {
            var (uid, _, organizationId) = await SeedViewerAsync(fixture, SpecApplications.BetaModuleId);

            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task An_expired_grant_denies_without_any_write()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantApplicationRoleAsync(
                userId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId,
                expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task A_deactivated_organization_denies_with_reason_organization_deactivated()
        {
            var (uid, _, organizationId) = await SeedViewerAsync(fixture, SpecApplications.AlphaModuleId);
            var organization = await fixture.DbContext.Organizations.SingleAsync(o => o.Id == organizationId);
            organization.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("organization_deactivated", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Sad paths, kept separate.

    public class Given_an_application_the_org_does_not_have(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_guard_answers_four_hundred_four_with_reason_application_not_found_and_audits_it()
        {
            var (uid, _, organizationId) = await fixture.SeedMemberAsync();

            var response = await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.True(await fixture.DbContext.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EventType == IdentityAuditEventTypes.DeniedApplicationAccess && a.OrganizationId == organizationId));
        }

        [Fact]
        public async Task A_retired_application_is_not_found_either()
        {
            var (uid, _, organizationId) = await SeedViewerAsync(fixture, SpecApplications.AlphaModuleId, SpecApplications.OtherAppId);
            var application = await fixture.DbContext.CatalogApplications.SingleAsync(a => a.Id == SpecApplications.OtherAppId);
            application.ChangeStatus(CatalogStatus.Retired, DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"/api/_test/app-scoped/{organizationId}/{SpecApplications.OtherAppId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // Scopes never leak: an org-level grant, or a grant for another application, doesn't satisfy an app check.
    public class Given_grants_in_other_scopes(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_org_level_role_holding_the_permission_name_does_not_satisfy_the_app_check()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantPermissionsAsync(userId, organizationId, SpecApplications.AlphaRead);

            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task A_grant_for_another_application_does_not_satisfy_it()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.EntitleAsync(organizationId, SpecApplications.OtherAppId, SpecApplications.GammaModuleId);
            await fixture.GrantApplicationRoleAsync(
                userId, await fixture.ApplicationRoleIdAsync(SpecApplications.OtherAppAdministrator), organizationId, SpecApplications.OtherAppId);

            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task A_grant_in_another_organization_does_not_satisfy_it()
        {
            var (uid, userId, orgA) = await SeedViewerAsync(fixture, SpecApplications.AlphaModuleId);
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgB);
            await fixture.EntitleAsync(orgB, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);

            Assert.Equal(HttpStatusCode.OK, (await EntitlementSpecs.AppProbeAsync(fixture, uid, orgA)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, uid, orgB)).StatusCode);
        }
    }

    /// <summary>A member of a new org that has SpecApp (and optionally another app) with the given SpecApp module enabled, holding SpecApp Viewer.</summary>
    private static async Task<(string Uid, int UserId, int OrganizationId)> SeedViewerAsync(
        IdentitySpecFixture fixture, int specAppModuleId, int? alsoEntitleApplicationId = null)
    {
        var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
        await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, specAppModuleId);
        if (alsoEntitleApplicationId is { } applicationId)
        {
            await fixture.EntitleAsync(organizationId, applicationId);
        }

        await fixture.GrantApplicationRoleAsync(
            userId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);

        return (uid, userId, organizationId);
    }
}
