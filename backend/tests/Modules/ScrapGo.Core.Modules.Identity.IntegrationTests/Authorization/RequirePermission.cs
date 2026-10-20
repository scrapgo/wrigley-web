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
            await fixture.GrantPermissionsAsync(userId, Permissions.AdminAccess);

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
            var (uid, _) = await fixture.SeedUserAsync(classification: UserClassification.Internal);

            var token = fixture.CreateToken(uid, extraClaims: [("permissions", SpecPermissions.Alpha)]);
            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_permission_whose_role_was_soft_deleted(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_request_is_denied()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);
            var role = await fixture.DbContext.Roles.SingleAsync(r => r.Id == roleId);
            role.MarkDeleted(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Until B6 resets the schema, the old tables can still hold an
    // organization-scoped grant. Roles are global now, so it must never resolve.
    public class Given_a_leftover_organization_scoped_grant(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task It_grants_nothing()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);
            var legacyScopeId = (await fixture.DbContext.Database.SqlQuery<int>($"""
                INSERT INTO identity.organizations (name, slug, status, created_at, updated_at)
                VALUES ('Legacy', {"legacy-" + Guid.NewGuid().ToString("N")}, 'Active', now(), now())
                RETURNING id AS "Value"
                """).ToListAsync()).Single();
            await fixture.DbContext.Database.ExecuteSqlAsync(
                $"UPDATE identity.user_roles SET organization_id = {legacyScopeId} WHERE user_id = {userId} AND role_id = {roleId}");

            var response = await fixture.SendAsync(HttpMethod.Get, PermissionScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
