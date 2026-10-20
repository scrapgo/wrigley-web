// B10: POST /api/session creates an Internal user only for a verified Google
// Workspace sign-in on an allow-listed hd. Everything else is 403 no_access, with no row.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class HdAllowListClassification
{
    public class Given_the_allow_list_contains_example_dot_com(AllowlistedHdIdentitySpecFixture fixture)
        : IClassFixture<AllowlistedHdIdentitySpecFixture>
    {
        [Fact]
        public async Task A_verified_google_sign_in_with_hd_example_dot_com_creates_an_internal_scrapgo_user()
        {
            var uid = Guid.NewGuid().ToString();

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid, hd: "example.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var user = await fixture.DbContext.Users.AsNoTracking().SingleAsync(u => u.IdentityPlatformUid == uid);
            Assert.Equal(UserType.Internal, user.UserType);
            Assert.Equal(StartSessionHandler.GoogleProvider, user.SignInProvider);
            Assert.Equal([DefaultRoleNames.ScrapGoUser], await RoleKeysOfAsync(fixture, user.Id));
        }

        [Fact]
        public async Task The_allow_list_comparison_is_case_insensitive()
        {
            var uid = Guid.NewGuid().ToString();

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid, hd: "EXAMPLE.COM"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(UserType.Internal, await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.IdentityPlatformUid == uid).Select(u => u.UserType).SingleAsync());
        }

        // Sad paths, kept separate.

        [Fact]
        public async Task A_google_sign_in_with_hd_other_dot_com_is_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateWorkspaceToken(uid, hd: "other.com"));

        [Fact]
        public async Task A_google_sign_in_with_no_hd_claim_is_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateToken(
                uid, email: "someone@gmail.example", signInProvider: StartSessionHandler.GoogleProvider, emailVerified: true));

        // A password account on the company domain is not a Workspace sign-in,
        // even if its token somehow carried hd.
        [Fact]
        public async Task A_password_sign_in_on_the_company_domain_is_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateToken(
                uid, email: $"{uid}@example.com", hd: "example.com", signInProvider: "password", emailVerified: true));

        [Fact]
        public async Task A_google_sign_in_with_an_unverified_email_is_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateToken(
                uid, email: $"{uid}@example.com", hd: "example.com", signInProvider: StartSessionHandler.GoogleProvider, emailVerified: false));

        [Fact]
        public async Task A_google_sign_in_without_an_email_verified_claim_is_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateToken(
                uid, email: $"{uid}@example.com", hd: "example.com", signInProvider: StartSessionHandler.GoogleProvider));

        [Fact]
        public async Task A_refusal_writes_an_access_denied_audit_row_with_reason_no_access()
        {
            var uid = Guid.NewGuid().ToString();

            await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid, hd: "other.com"));

            // Metadata is jsonb, so the uid is matched client-side.
            var denials = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.AccessDenied && a.UserId == null)
                .ToListAsync();
            var audit = Assert.Single(denials, a => a.Metadata!.Contains(uid));
            Assert.Null(audit.UserId);
            Assert.Equal("no_access", ReasonOf(audit.Metadata));
        }
    }

    public class Given_a_user_already_exists_as_external(AllowlistedHdIdentitySpecFixture fixture)
        : IClassFixture<AllowlistedHdIdentitySpecFixture>
    {
        // UserType is decided when the user is created, never re-evaluated at sign-in.
        [Fact]
        public async Task A_workspace_sign_in_on_an_allow_listed_hd_does_not_reclassify_them()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid, hd: "example.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(UserType.External, await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.Id == userId).Select(u => u.UserType).SingleAsync());
            Assert.Empty(await RoleKeysOfAsync(fixture, userId));
        }
    }

    public class Given_no_allow_list_is_configured(NoAllowlistIdentitySpecFixture fixture) : IClassFixture<NoAllowlistIdentitySpecFixture>
    {
        [Fact]
        public async Task A_verified_workspace_sign_in_is_still_refused() =>
            await AssertNoAccessAsync(fixture, uid => fixture.CreateWorkspaceToken(uid, hd: "example.com"));
    }

    private static async Task AssertNoAccessAsync(IdentitySpecFixture fixture, Func<string, string> tokenFor)
    {
        var uid = Guid.NewGuid().ToString();

        var response = await fixture.StartSessionAsync(tokenFor(uid));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no_access", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        Assert.False(await fixture.DbContext.Users.AsNoTracking().AnyAsync(u => u.IdentityPlatformUid == uid));
    }

    private static string? ReasonOf(string? metadata)
    {
        using var document = System.Text.Json.JsonDocument.Parse(metadata!);
        return document.RootElement.GetProperty("reason").GetString();
    }

    private static Task<List<string>> RoleKeysOfAsync(IdentitySpecFixture fixture, int userId) =>
        fixture.DbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(fixture.DbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Key)
            .ToListAsync();
}
