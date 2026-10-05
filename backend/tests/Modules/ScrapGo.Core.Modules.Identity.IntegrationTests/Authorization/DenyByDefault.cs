// ADMIN-API-STATUS Task 10: deny-by-default at the framework level, independent of [Authorize].
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class DenyByDefault
{
    // The fallback authorization policy covers an action that forgot every attribute.
    public class Given_an_action_with_no_authorization_attribute(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_anonymous_request_gets_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync("/api/_test/no-attribute");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task An_authenticated_request_is_allowed()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/_test/no-attribute", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // The only intended anonymous endpoints: Cloud Run's probes.
    public class Given_the_health_endpoints(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/healthz")]
        [InlineData("/healthz/ready")]
        public async Task They_stay_anonymous(string path)
        {
            var response = await fixture.Client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Signing in, and joining an organization, grant nothing.
    public class Given_a_user_who_signed_in_and_was_added_to_an_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_hold_no_role_and_every_permission_check_denies()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var uid = Guid.NewGuid().ToString();
            var token = fixture.CreateToken(uid, email: "joiner@example.com");
            (await fixture.SendUsersMeAsync(token)).EnsureSuccessStatusCode();
            var userId = await fixture.DbContext.Users.AsNoTracking().Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            (await fixture.SendAsync(HttpMethod.Post, $"/api/organizations/{organizationId}/members/{userId}", fixture.CreateToken(adminUid)))
                .EnsureSuccessStatusCode();

            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId));
            Assert.Equal(HttpStatusCode.Forbidden,
                (await fixture.SendAsync(HttpMethod.Get, $"/api/_test/permission-scoped/{organizationId}", token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", token)).StatusCode);
        }
    }
}
