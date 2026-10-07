// ORG-APP-MODULE-MODEL Task 3: a platform administrator deactivates and reactivates organizations.
// Deactivation blocks all organization-scoped access without deleting anything.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class OrganizationLifecycle
{
    public class Given_an_organization_with_a_member_and_a_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Deactivating_denies_the_members_next_request_with_reason_organization_deactivated()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (memberUid, memberId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(memberId, organizationId, SpecPermissions.Alpha);
            Assert.Equal(HttpStatusCode.OK, (await ProbeAsync(fixture, memberUid, organizationId)).StatusCode);

            var response = await DeactivateAsync(fixture, platformUid, organizationId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var next = await ProbeAsync(fixture, memberUid, organizationId);
            Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
            Assert.Equal("organization_deactivated", await IdentitySpecFixture.ReadProblemReasonAsync(next));
        }

        [Fact]
        public async Task Deactivating_removes_the_org_from_me_and_my_organizations_but_deletes_nothing()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (memberUid, memberId, organizationId) = await fixture.SeedMemberAsync();
            var roleId = await fixture.GrantPermissionsAsync(memberId, organizationId, SpecPermissions.Alpha);

            (await DeactivateAsync(fixture, platformUid, organizationId)).EnsureSuccessStatusCode();

            var me = await (await fixture.SendUsersMeAsync(fixture.CreateToken(memberUid, email: "member@example.com")))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, me.GetProperty("permissions").GetArrayLength());
            var mine = await (await fixture.SendAsync(HttpMethod.Get, "/api/organizations", fixture.CreateToken(memberUid)))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, mine.GetArrayLength());

            Assert.True(await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == memberId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active));
            Assert.True(await fixture.DbContext.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.UserId == memberId && ur.RoleId == roleId && ur.OrganizationId == organizationId));
        }

        [Fact]
        public async Task Reactivating_restores_access_exactly_as_it_was()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (memberUid, memberId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(memberId, organizationId, SpecPermissions.Alpha);
            (await DeactivateAsync(fixture, platformUid, organizationId)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(
                HttpMethod.Post, $"/api/admin/organizations/{organizationId}/reactivate", fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ProbeAsync(fixture, memberUid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task Deactivating_twice_is_idempotent_with_one_audit_entry()
        {
            var (platformUid, platformAdminId) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            (await DeactivateAsync(fixture, platformUid, organizationId)).EnsureSuccessStatusCode();

            var response = await DeactivateAsync(fixture, platformUid, organizationId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var audit = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.OrganizationDeactivated && a.OrganizationId == organizationId)
                .ToListAsync());
            Assert.Equal(platformAdminId, audit.UserId);
        }

        // The platform administrator acts from outside: the membership guard doesn't apply to /api/admin.
        [Fact]
        public async Task A_platform_admin_who_is_not_a_member_can_deactivate()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            Assert.Equal(HttpStatusCode.NoContent, (await DeactivateAsync(fixture, platformUid, organizationId)).StatusCode);
        }

        [Fact]
        public async Task An_unknown_organization_returns_four_hundred_four()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await DeactivateAsync(fixture, platformUid, 999_999)).StatusCode);
        }
    }

    public class Given_a_platform_administrator_listing_organizations(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_admin_organizations_lists_organizations_they_are_not_a_member_of()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/organizations?pageSize=100", fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray();
            Assert.Contains(items, o => o.GetProperty("id").GetInt32() == organizationId);
        }

        [Fact]
        public async Task An_unknown_status_filter_returns_four_hundred_with_reason_invalid_request()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/organizations?status=Archived", fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Sad paths, kept separate.

    // An org admin is not a platform admin, even of their own organization.
    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("GET", "/api/admin/organizations")]
        [InlineData("POST", "/api/admin/organizations/{0}/deactivate")]
        [InlineData("POST", "/api/admin/organizations/{0}/reactivate")]
        public async Task Every_platform_organization_route_returns_four_hundred_three(string method, string pathFormat)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(new HttpMethod(method), string.Format(pathFormat, organizationId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(OrganizationStatus.Active, await fixture.DbContext.Organizations.AsNoTracking()
                .Where(o => o.Id == organizationId).Select(o => o.Status).SingleAsync());
        }
    }

    private static Task<HttpResponseMessage> DeactivateAsync(IdentitySpecFixture fixture, string platformUid, int organizationId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/admin/organizations/{organizationId}/deactivate", fixture.CreateToken(platformUid));

    private static Task<HttpResponseMessage> ProbeAsync(IdentitySpecFixture fixture, string uid, int organizationId) =>
        fixture.SendAsync(HttpMethod.Get, $"/api/_test/permission-scoped/{organizationId}", fixture.CreateToken(uid));
}
