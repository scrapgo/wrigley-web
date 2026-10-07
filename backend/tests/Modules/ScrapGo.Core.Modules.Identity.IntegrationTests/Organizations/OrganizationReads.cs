// ADMIN-API-STATUS Task 6: organization detail and members under {organizationId}, gated on User.Read there.
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class OrganizationReads
{
    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_organization_returns_its_detail()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, colleagueId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(colleagueId, organizationId);

            var response = await fixture.SendAsync(HttpMethod.Get, OrganizationPath(organizationId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(organizationId, body.GetProperty("id").GetInt32());
            Assert.Equal("Active", body.GetProperty("status").GetString());
            Assert.Equal(2, body.GetProperty("activeMemberCount").GetInt32());
        }

        [Fact]
        public async Task Get_members_lists_them_with_their_roles_in_this_org()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"{OrganizationPath(organizationId)}/members", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var member = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
            Assert.Equal(adminId, member.GetProperty("userId").GetInt32());
            var role = Assert.Single(member.GetProperty("roles").EnumerateArray());
            Assert.Equal(DefaultRoleNames.OrganizationAdministrator, role.GetProperty("name").GetString());
            Assert.Equal(organizationId, role.GetProperty("organizationId").GetInt32());
        }
    }

    // Permission-based: any role carrying User.Read in the organization is enough.
    public class Given_a_member_holding_user_read_through_a_custom_role(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_organization_and_members_are_allowed()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.UserRead);

            Assert.Equal(HttpStatusCode.OK,
                (await fixture.SendAsync(HttpMethod.Get, OrganizationPath(organizationId), fixture.CreateToken(uid))).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await fixture.SendAsync(HttpMethod.Get, $"{OrganizationPath(organizationId)}/members", fixture.CreateToken(uid))).StatusCode);
        }
    }

    public class Given_more_members_than_one_page(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_members_pages_by_user_id_with_the_total()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberIds = new List<int> { adminId };
            for (var i = 0; i < 2; i++)
            {
                var (_, userId) = await fixture.SeedUserAsync();
                await fixture.SeedMembershipAsync(userId, organizationId);
                memberIds.Add(userId);
            }

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"{OrganizationPath(organizationId)}/members?page=2&pageSize=2", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
            Assert.Equal([memberIds[2]], body.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("userId").GetInt32()));
        }

        [Theory]
        [InlineData("page=0")]
        [InlineData("pageSize=101")]
        public async Task An_invalid_page_returns_four_hundred_with_reason_invalid_request(string query)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"{OrganizationPath(organizationId)}/members?{query}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Sad paths, kept separate.

    public class Given_a_member_without_user_read(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/members")]
        public async Task Each_read_returns_four_hundred_three(string suffix)
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha);

            var response = await fixture.SendAsync(HttpMethod.Get, OrganizationPath(organizationId) + suffix, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // The membership guard runs before the permission check: being an admin of
    // org A is no way into org B.
    public class Given_an_admin_of_org_a_reading_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/members")]
        public async Task Each_read_is_stopped_by_the_membership_guard(string suffix)
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var orgB = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, OrganizationPath(orgB) + suffix, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("no_active_membership", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // An unknown organization answers exactly like an existing one the caller
    // isn't in, so organization ids can't be probed for.
    public class Given_an_organization_id_that_does_not_exist(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_organization_returns_the_same_four_hundred_three_as_a_foreign_org()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, OrganizationPath(999_999), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("no_active_membership", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // Below the guard, the handler itself reports NotFound (mapped to 404).
        [Fact]
        public async Task The_read_service_reports_not_found()
        {
            await using var scope = fixture.Services.CreateAsyncScope();

            var result = await scope.ServiceProvider.GetRequiredService<OrganizationReadService>()
                .GetAsync(999_999, CancellationToken.None);

            Assert.Equal(OrganizationReadOutcome.NotFound, result.Outcome);
        }
    }

    // Platform grants don't open organization-scoped routes: the guard needs a membership.
    public class Given_a_platform_administrator_who_is_not_a_member(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_organization_returns_four_hundred_three()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId, organizationId: null);
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, OrganizationPath(organizationId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/members")]
        public async Task Each_read_returns_four_hundred_one(string suffix)
        {
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.Client.GetAsync(OrganizationPath(organizationId) + suffix);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static string OrganizationPath(int organizationId) => $"/api/organizations/{organizationId}";
}
