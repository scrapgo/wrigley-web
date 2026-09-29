// STORY-011: Disabled user denied despite a valid token.
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
            fixture.DbContext.Users.Add(User.Provision(_uid, "active@example.com", UserClassification.External, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            _response = await fixture.SendUsersMeAsync(fixture.CreateToken(_uid));
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void The_response_is_two_hundred() => Assert.Equal(HttpStatusCode.OK, _response.StatusCode);
    }

    // A never-seen UID has nothing to disable, so "no row" must not be treated as "denied".
    public class Given_a_uid_with_no_user_row_yet_and_a_valid_jwt(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_two_hundred()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(Guid.NewGuid().ToString()));

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
            var user = User.Provision(_uid, "disabled@example.com", UserClassification.External, now);
            user.Disable(now);
            fixture.DbContext.Users.Add(user);
            await fixture.DbContext.SaveChangesAsync();
            _userId = user.Id;

            _response = await fixture.SendUsersMeAsync(fixture.CreateToken(_uid));
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void An_authenticated_call_returns_four_hundred_three() =>
            Assert.Equal(HttpStatusCode.Forbidden, _response.StatusCode);

        [Fact]
        public async Task The_response_body_carries_the_user_disabled_reason() =>
            Assert.Equal(DisabledUserGateMiddleware.DisabledUserReason, await IdentitySpecFixture.ReadProblemReasonAsync(_response));

        [Fact]
        public async Task An_audit_log_row_of_type_denied_disabled_user_is_written() =>
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.DeniedDisabledUser && a.UserId == _userId));
    }

    // The gate never turns a missing token into a 403: that stays the ordinary 401.
    public class Given_an_unauthenticated_request_to_a_protected_endpoint(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one_not_four_hundred_three()
        {
            var response = await fixture.Client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
