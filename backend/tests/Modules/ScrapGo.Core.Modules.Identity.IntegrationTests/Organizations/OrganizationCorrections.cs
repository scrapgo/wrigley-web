// Platform admins fix mistakes in an organization: rename it, set its
// administrator, or delete it permanently once deactivated.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class OrganizationCorrections
{
    public class Given_a_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Renaming_works_without_membership_and_regenerates_the_slug()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            var name = $"Midwest Metals {organizationId}";

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}", fixture.CreateToken(uid), new { name });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var organization = await fixture.DbContext.Organizations.AsNoTracking().SingleAsync(o => o.Id == organizationId);
            Assert.Equal(name, organization.Name);
            Assert.Equal($"midwest-metals-{organizationId}", organization.Slug);
        }

        [Fact]
        public async Task Renaming_to_another_organizations_name_returns_four_hundred_nine()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var taken = await fixture.SeedOrganizationAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            var takenName = await fixture.DbContext.Organizations.AsNoTracking().Where(o => o.Id == taken).Select(o => o.Name).SingleAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}", fixture.CreateToken(uid), new { name = takenName });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("duplicate_slug", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Renaming_to_a_name_without_letters_or_digits_returns_four_hundred()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}", fixture.CreateToken(uid), new { name = "  --  " });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_name", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // The case that prompted this: created with an email, invitation never accepted, so no members.
        [Fact]
        public async Task Setting_the_administrator_fixes_an_organization_stuck_on_an_invitation()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, userId) = await fixture.SeedUserAsync("first.admin@example.com");
            var created = await fixture.SendAsync(
                HttpMethod.Post, "/api/admin/organizations", fixture.CreateToken(uid), new { name = "Stuck Org", firstAdminEmail = "nobody@example.com" });
            var organizationId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}/administrators/{userId}", fixture.CreateToken(uid));
            var again = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}/administrators/{userId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
            Assert.True(await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active));
            Assert.Single(await fixture.DbContext.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == userId && ur.OrganizationId == organizationId).ToListAsync());
            Assert.Equal(InvitationStatus.Revoked, (await fixture.DbContext.Invitations.AsNoTracking()
                .SingleAsync(i => i.OrganizationId == organizationId)).Status);
        }

        [Fact]
        public async Task Setting_an_unknown_user_returns_four_hundred_four()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Put, $"/api/admin/organizations/{organizationId}/administrators/999999", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("user_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_active_organization_cannot_be_deleted()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(HttpMethod.Delete, $"/api/admin/organizations/{organizationId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("organization_active", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.True(await fixture.DbContext.Organizations.AsNoTracking().AnyAsync(o => o.Id == organizationId));
        }

        [Fact]
        public async Task Deleting_a_deactivated_organization_removes_everything_in_it_but_keeps_its_audit_history()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (_, memberId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantApplicationRoleAsync(
                memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);
            await fixture.SendAsync(
                HttpMethod.Post, $"/api/organizations/{organizationId}/invitations", fixture.CreateToken(uid),
                new { email = "later@example.com", grants = Array.Empty<object>() });
            (await fixture.SendAsync(HttpMethod.Post, $"/api/admin/organizations/{organizationId}/deactivate", fixture.CreateToken(uid)))
                .EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Delete, $"/api/admin/organizations/{organizationId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await fixture.DbContext.Organizations.AsNoTracking().AnyAsync(o => o.Id == organizationId));
            Assert.False(await fixture.DbContext.OrganizationMemberships.AsNoTracking().AnyAsync(m => m.OrganizationId == organizationId));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.OrganizationId == organizationId));
            Assert.False(await fixture.DbContext.Invitations.AsNoTracking().AnyAsync(i => i.OrganizationId == organizationId));
            Assert.True(await fixture.DbContext.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.OrganizationId == organizationId && a.EventType == IdentityAuditEventTypes.OrganizationDeleted));
            Assert.True(await fixture.DbContext.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.OrganizationId == organizationId && a.EventType == IdentityAuditEventTypes.OrganizationDeactivated));
        }
    }

    public class Given_an_organization_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("PUT", "")]
        [InlineData("PUT", "/administrators/1")]
        [InlineData("DELETE", "")]
        public async Task The_platform_correction_routes_return_four_hundred_three(string method, string suffix)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                new HttpMethod(method), $"/api/admin/organizations/{organizationId}{suffix}", fixture.CreateToken(uid), new { name = "Mine now" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
