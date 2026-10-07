// ORG-APP-MODULE-MODEL Task 5: the application catalog is code-defined; platform admins only retire or reactivate.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Applications;

public class ApplicationCatalogSpecs
{
    public class Given_any_signed_in_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_catalog_lists_applications_with_modules_and_their_permissions()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/catalog/applications", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var specApp = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Single(a => a.GetProperty("id").GetInt32() == SpecApplications.SpecAppId);
            Assert.Equal("spec-app", specApp.GetProperty("key").GetString());
            var alpha = specApp.GetProperty("modules").EnumerateArray()
                .Single(m => m.GetProperty("id").GetInt32() == SpecApplications.AlphaModuleId);
            Assert.Equal(
                [SpecApplications.AlphaRead, SpecApplications.AlphaWrite],
                alpha.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }

        // The catalog is a compile-time contract: ids unique, permissions named
        // {App}.{Module}.{Action} and in Permissions.All, templates using only
        // their own application's permissions (plus Application.ManageAccess).
        [Fact]
        public void The_code_catalog_is_consistent()
        {
            var applications = ApplicationCatalog.All;
            var modules = applications.SelectMany(a => a.Modules).ToList();

            Assert.Equal(applications.Count, applications.Select(a => a.Id).Distinct().Count());
            Assert.Equal(applications.Count, applications.Select(a => a.Key).Distinct().Count());
            Assert.Equal(modules.Count, modules.Select(m => m.Id).Distinct().Count());
            Assert.DoesNotContain(applications, a => a.Id >= SpecApplications.SpecAppId);
            Assert.DoesNotContain(modules, m => m.Id >= SpecApplications.SpecAppId);

            foreach (var application in applications)
            {
                var own = application.Modules.SelectMany(m => m.Permissions).ToList();
                Assert.All(own, p => Assert.StartsWith(application.Name + ".", p, StringComparison.Ordinal));
                Assert.All(own, p => Assert.Contains(p, Permissions.All));
                Assert.Contains(application.RoleTemplates, t => t.Name == $"{application.Name} Administrator");
                Assert.All(
                    application.RoleTemplates.SelectMany(t => t.Permissions),
                    p => Assert.True(own.Contains(p) || p == Permissions.ApplicationManageAccess, p));
            }
        }

        // The migration seeds the templates the code defines, with exactly those permissions.
        [Fact]
        public async Task The_downstream_role_templates_are_seeded_as_defined()
        {
            foreach (var template in DownstreamApplication.Definition.RoleTemplates)
            {
                var role = await fixture.DbContext.Roles.AsNoTracking()
                    .SingleAsync(r => r.Name == template.Name && r.OrganizationId == null);
                var permissions = await fixture.DbContext.RolePermissions.AsNoTracking()
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.Permission.Name)
                    .ToListAsync();

                Assert.Equal(DownstreamApplication.Id, role.ApplicationId);
                Assert.Equal(template.Permissions.Order(StringComparer.Ordinal), permissions.Order(StringComparer.Ordinal));
            }
        }
    }

    public class Given_a_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Retiring_an_application_is_audited_idempotent_and_reversible()
        {
            var (uid, platformAdminId) = await fixture.SeedPlatformAdministratorAsync();
            var path = $"/api/catalog/applications/{SpecApplications.OtherAppId}";

            var retire = await fixture.SendAsync(HttpMethod.Put, path, fixture.CreateToken(uid), new { status = "Retired" });
            var again = await fixture.SendAsync(HttpMethod.Put, path, fixture.CreateToken(uid), new { status = "retired" });

            Assert.Equal(HttpStatusCode.NoContent, retire.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
            Assert.Equal(CatalogStatus.Retired, await fixture.DbContext.CatalogApplications.AsNoTracking()
                .Where(a => a.Id == SpecApplications.OtherAppId).Select(a => a.Status).SingleAsync());
            var audit = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.CatalogApplicationChanged).ToListAsync());
            Assert.Equal(platformAdminId, audit.UserId);

            (await fixture.SendAsync(HttpMethod.Put, path, fixture.CreateToken(uid), new { status = "Active" })).EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task An_unknown_status_returns_four_hundred_and_an_unknown_module_four_hundred_four()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var badStatus = await fixture.SendAsync(
                HttpMethod.Put, $"/api/catalog/applications/{SpecApplications.SpecAppId}", fixture.CreateToken(uid), new { status = "Deleted" });
            var foreignModule = await fixture.SendAsync(
                HttpMethod.Put, $"/api/catalog/applications/{SpecApplications.SpecAppId}/modules/{SpecApplications.GammaModuleId}",
                fixture.CreateToken(uid), new { status = "Retired" });

            Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, foreignModule.StatusCode);
        }
    }

    // Sad path, kept separate.

    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Changing_catalog_status_returns_four_hundred_three()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/catalog/applications/{SpecApplications.SpecAppId}", fixture.CreateToken(uid), new { status = "Retired" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
