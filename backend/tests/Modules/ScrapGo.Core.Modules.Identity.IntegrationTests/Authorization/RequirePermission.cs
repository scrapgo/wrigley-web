// B8: [RequirePermission] builds perm: policies checked against the caller's
// access context. A denial is a 403 ProblemDetails (missing_permission) and
// an access.denied audit row.
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class RequirePermission
{
    private const string PermissionScopedPath = "/api/_test/permission-scoped";

    private const string AnyOfPath = "/api/_test/any-of";

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
        public async Task A_seeded_role_satisfies_a_seeded_permission()
        {
            var (uid, _) = await fixture.SeedPlatformAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // perm:a|b needs any one of them.
    public class Given_an_any_of_requirement(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData(SpecPermissions.Alpha)]
        [InlineData(SpecPermissions.Beta)]
        public async Task Either_permission_is_enough(string held)
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, held);

            var response = await fixture.SendAsync(HttpMethod.Get, AnyOfPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Neither_is_denied()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, SpecPermissions.Gamma);

            var response = await fixture.SendAsync(HttpMethod.Get, AnyOfPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_caller_who_lacks_the_required_permission(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_gets_four_hundred_three_with_reason_missing_permission()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, SpecPermissions.Beta);

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task The_denial_is_audited_as_access_denied()
        {
            var (uid, userId) = await fixture.SeedUserAsync();

            await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            var audit = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.AccessDenied && a.UserId == userId)
                .ToListAsync());
            using var metadata = JsonDocument.Parse(audit.Metadata!);
            Assert.Equal(PermissionScopedPath, metadata.RootElement.GetProperty("path").GetString());
            Assert.Equal([SpecPermissions.Alpha], metadata.RootElement.GetProperty("required").EnumerateArray().Select(p => p.GetString()));
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

    // A toggle never grants access, but turning a feature Off takes its permissions away.
    public class Given_the_owning_feature_is_off(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_granted_permission_is_denied()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);

            // The spec permissions belong to the dashboard feature.
            await fixture.DbContext.Database.ExecuteSqlAsync($"UPDATE identity.features SET state = 'Off' WHERE key = {FeatureKeys.Dashboard}");
            fixture.EvictAccess();

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_unknown_uid(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_denied()
        {
            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(Guid.NewGuid().ToString()));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }
}
