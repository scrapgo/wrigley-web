// STORY-018/019: The [RequirePermission] engine, organization- and platform-scoped.
using ScrapGo.Core.Modules.Identity.Api.Authorization;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class RequirePermission
{
    public class Given_a_caller_who_holds_the_required_permission_in_the_target_org(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_allowed()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.InvoiceRead);

            var response = await SendPermissionScopedAsync(fixture, fixture.CreateToken(uid), organizationId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_a_caller_with_a_platform_scoped_grant(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_requirement_is_allowed()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.AdminAccess);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_caller_who_lacks_the_required_permission(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_denied_with_four_hundred_three()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.ReportRead);

            var response = await SendPermissionScopedAsync(fixture, fixture.CreateToken(uid), organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Organization context comes from the route, never from the token: a
    // forged organizationId claim pointing at the caller's real grant changes
    // nothing.
    public class Given_a_token_carrying_a_forged_organization_claim(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Resolution_uses_the_route_org_not_the_claim()
        {
            var (uid, userId, orgA) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, orgA, Permissions.InvoiceRead);
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgB);

            var token = fixture.CreateToken(uid, extraClaims: [("organizationId", orgA.ToString())]);
            var response = await SendPermissionScopedAsync(fixture, token, orgB);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_org_scoped_requirement_on_a_route_with_no_organization(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_returns_four_hundred_with_reason_organization_context_required()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.InvoiceRead);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/_test/permission-scoped-no-org", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                PermissionAuthorizationHandler.OrganizationContextRequiredReason,
                await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // A platform-scoped check is satisfied only by an assignment with no
    // organization. Holding the permission inside an organization isn't enough.
    public class Given_a_caller_holding_the_permission_only_at_org_level(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_requirement_is_denied()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.AdminAccess);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_permission_whose_role_was_soft_deleted(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_denied()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.InvoiceRead);
            var role = await fixture.DbContext.Roles.SingleAsync(r => r.Id == roleId);
            role.MarkDeleted(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await SendPermissionScopedAsync(fixture, fixture.CreateToken(uid), organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    internal static Task<HttpResponseMessage> SendPermissionScopedAsync(IdentitySpecFixture fixture, string bearerToken, int organizationId) =>
        fixture.SendAsync(HttpMethod.Get, $"/api/_test/permission-scoped/{organizationId}", bearerToken);
}
