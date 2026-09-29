// STORY-014: Cross-tenant (IDOR) guard on every {organizationId} route.
using ScrapGo.Core.Modules.Identity.Api.Middleware;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class CrossTenantIdorGuard
{
    public class Given_a_user_with_active_membership_in_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_request_on_an_org_b_route_returns_two_hundred()
        {
            var (uid, _, orgB) = await fixture.SeedMemberAsync();

            var response = await SendOrgScopedAsync(fixture, HttpMethod.Get, uid, orgB);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_user_with_no_membership_in_org_b(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private string _uid = null!;
        private int _userId;
        private int _orgB;

        public async Task InitializeAsync()
        {
            (_uid, _userId) = await fixture.SeedUserAsync();
            _orgB = await fixture.SeedOrganizationAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        public static TheoryData<string> Methods => ["GET", "POST", "PUT", "DELETE"];

        [Theory]
        [MemberData(nameof(Methods))]
        public async Task Every_method_on_an_org_b_route_returns_four_hundred_three_with_reason_no_active_membership(string method)
        {
            var response = await SendOrgScopedAsync(fixture, new HttpMethod(method), _uid, _orgB);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(OrganizationMembershipGuardMiddleware.NoActiveMembershipReason, await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task The_denial_is_audit_logged()
        {
            await SendOrgScopedAsync(fixture, HttpMethod.Get, _uid, _orgB);

            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(a =>
                a.EventType == IdentityAuditEventTypes.DeniedCrossTenantAccess && a.UserId == _userId && a.OrganizationId == _orgB));
        }
    }

    public class Given_a_user_whose_only_membership_in_org_b_is_disabled(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_org_b_route_returns_four_hundred_three()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgB, disabled: true);

            var response = await SendOrgScopedAsync(fixture, HttpMethod.Get, uid, orgB);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Membership in A must not open B, even for A's administrator.
    public class Given_an_org_a_administrator_with_no_membership_in_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_org_b_route_returns_four_hundred_three()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var orgB = await fixture.SeedOrganizationAsync();

            var response = await SendOrgScopedAsync(fixture, HttpMethod.Get, uid, orgB);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // The guard never turns a missing token into a 403: that stays the ordinary 401.
    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_org_scoped_route_returns_four_hundred_one()
        {
            var orgB = await fixture.SeedOrganizationAsync();

            var response = await fixture.Client.GetAsync($"/api/_test/org-scoped/{orgB}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> SendOrgScopedAsync(
        IdentitySpecFixture fixture, HttpMethod method, string uid, int organizationId) =>
        fixture.SendAsync(method, $"/api/_test/org-scoped/{organizationId}", fixture.CreateToken(uid));
}
