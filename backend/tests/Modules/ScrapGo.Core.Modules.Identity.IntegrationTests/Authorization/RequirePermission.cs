// STORY-018/019: The [RequirePermission] engine. Roles are global, so every check is unscoped.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class RequirePermission
{
    private const string PermissionScopedPath = "/api/_test/permission-scoped";

    public class Given_a_caller_who_holds_the_required_permission(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_allowed()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task An_admin_access_requirement_is_allowed_with_admin_access()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, Permissions.UsersManage);

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
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, SpecPermissions.Beta);

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Permissions come from the database, never from the token: a forged
    // permission claim changes nothing.
    public class Given_a_token_carrying_a_forged_permission_claim(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_claim_is_ignored()
        {
            var (uid, _) = await fixture.SeedUserAsync(userType: UserType.Internal);

            var token = fixture.CreateToken(uid, extraClaims: [("permissions", SpecPermissions.Alpha)]);
            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
