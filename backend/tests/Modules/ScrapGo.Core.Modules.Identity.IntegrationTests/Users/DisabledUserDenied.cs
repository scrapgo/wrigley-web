// STORY-011, B10: a disabled user is denied despite a valid token: 403 account_disabled.
using ScrapGo.Core.Modules.Identity.Api.Middleware;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class DisabledUserDenied
{
    // Happy paths first: the gate must not interfere with an Active user, nor
    // block a caller whose user row doesn't exist yet.

    public class Given_an_active_user_with_a_valid_jwt(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            fixture.DbContext.Users.Add(User.Provision(_uid, "active@example.com", UserType.External, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            _response = await fixture.StartSessionAsync(fixture.CreateToken(_uid));
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void Starting_a_session_returns_two_hundred() => Assert.Equal(HttpStatusCode.OK, _response.StatusCode);
    }

    // A never-seen UID has nothing to disable, so "no row" must not be treated as "disabled".
    public class Given_a_uid_with_no_user_row_yet_and_a_valid_jwt(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_gate_lets_the_request_through()
        {
            var response = await fixture.SendAuthenticatedProbeAsync(fixture.CreateToken(Guid.NewGuid().ToString()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // The gate never runs, nor touches the DB, for an unauthenticated, unprotected route.
    public class Given_a_request_to_healthz_with_no_bearer_token(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_two_hundred()
        {
            var response = await fixture.Client.GetAsync("/healthz");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_disabled_user_with_a_valid_jwt(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private int _userId;
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var user = User.Provision(_uid, $"disabled-{_uid}@scrapgo.example", UserType.Internal, now);
            user.Disable(now);
            fixture.DbContext.Users.Add(user);
            await fixture.DbContext.SaveChangesAsync();
            _userId = user.Id;

            // Even a sign-in that passes the internal rule is refused.
            _response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(_uid));
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void Starting_a_session_returns_four_hundred_three() =>
            Assert.Equal(HttpStatusCode.Forbidden, _response.StatusCode);

        [Fact]
        public async Task The_response_body_carries_the_account_disabled_reason() =>
            Assert.Equal("account_disabled", await IdentitySpecFixture.ReadProblemReasonAsync(_response));

        [Fact]
        public async Task An_access_denied_audit_row_with_reason_account_disabled_is_written()
        {
            var audits = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.AccessDenied && a.UserId == _userId)
                .ToListAsync();

            Assert.Contains(audits, a =>
            {
                using var document = System.Text.Json.JsonDocument.Parse(a.Metadata!);
                return document.RootElement.GetProperty("reason").GetString() == "account_disabled";
            });
        }

        [Fact]
        public async Task No_sign_in_is_recorded()
        {
            Assert.Null(await fixture.DbContext.Users.AsNoTracking().Where(u => u.Id == _userId).Select(u => u.LastSignInAt).SingleAsync());
            Assert.False(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.SessionStarted && a.UserId == _userId));
        }

        [Fact]
        public async Task Me_returns_four_hundred_three_account_disabled()
        {
            var response = await fixture.SendMeAsync(fixture.CreateToken(_uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(DisabledUserGateMiddleware.DisabledUserReason, await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // The gate never turns a missing token into a 403: that stays the ordinary 401.
    public class Given_an_unauthenticated_request_to_a_protected_endpoint(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Me_returns_four_hundred_one_not_four_hundred_three() =>
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/me")).StatusCode);

        [Fact]
        public async Task Session_returns_four_hundred_one_not_four_hundred_three() =>
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsync("/api/session", content: null)).StatusCode);
    }
}
