// STORY-048: Self-service secure account linking.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class SecureAccountLinking
{
    public class Given_a_fresh_reauth_token_for_the_callers_own_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private int _userId;
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            _userId = await SeedActiveUserAsync(fixture, _uid, "linker@example.com");

            var reauthToken = fixture.CreateToken(_uid, authTime: DateTimeOffset.UtcNow, signInProvider: "google.com");
            _response = await fixture.SendLinkProviderAsync(fixture.CreateToken(_uid), reauthToken);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void The_response_is_two_hundred() => Assert.Equal(HttpStatusCode.OK, _response.StatusCode);

        [Fact]
        public async Task An_audit_log_row_of_type_provider_linked_is_written() =>
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.ProviderLinked && a.UserId == _userId));

        [Fact]
        public async Task An_active_linked_credential_row_is_recorded_for_the_provider()
        {
            var credential = await fixture.DbContext.LinkedCredentials.AsNoTracking()
                .SingleAsync(lc => lc.UserId == _userId && lc.ProviderName == "google.com");

            Assert.Equal(LinkedCredentialStatus.Active, credential.Status);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_reauth_token_for_a_different_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _callerUid = Guid.NewGuid().ToString();
        private int _callerUserId;
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            _callerUserId = await SeedActiveUserAsync(fixture, _callerUid, "caller@example.com");

            // Same email as the caller, which proves linking is never resolved
            // by email match, only ever by UID.
            var reauthToken = fixture.CreateToken(
                Guid.NewGuid().ToString(), email: "caller@example.com", authTime: DateTimeOffset.UtcNow, signInProvider: "google.com");

            _response = await fixture.SendLinkProviderAsync(fixture.CreateToken(_callerUid), reauthToken);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task The_response_is_four_hundred_three_with_reason_reauth_identity_mismatch()
        {
            Assert.Equal(HttpStatusCode.Forbidden, _response.StatusCode);
            Assert.Equal("reauth_identity_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(_response));
        }

        [Fact]
        public async Task No_audit_log_row_of_type_provider_linked_is_written() =>
            Assert.Equal(0, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.ProviderLinked && a.UserId == _callerUserId));
    }

    public class Given_a_stale_reauth_token_for_the_callers_own_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            await SeedActiveUserAsync(fixture, _uid, "stale@example.com");

            // Well past the 5-minute freshness window, yet well inside the
            // 1-hour lifetime cap. This isolates freshness from ordinary expiry.
            var reauthToken = fixture.CreateToken(_uid, authTime: DateTimeOffset.UtcNow.AddMinutes(-30), signInProvider: "google.com");
            _response = await fixture.SendLinkProviderAsync(fixture.CreateToken(_uid), reauthToken);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task The_response_is_four_hundred_three_with_reason_reauth_not_fresh()
        {
            Assert.Equal(HttpStatusCode.Forbidden, _response.StatusCode);
            Assert.Equal("reauth_not_fresh", await IdentitySpecFixture.ReadProblemReasonAsync(_response));
        }
    }

    public class Given_a_reauth_token_that_fails_signature_validation(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            await SeedActiveUserAsync(fixture, _uid, "tampered@example.com");

            var reauthToken = fixture.CreateToken(_uid, authTime: DateTimeOffset.UtcNow) + "tampered";
            _response = await fixture.SendLinkProviderAsync(fixture.CreateToken(_uid), reauthToken);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task The_response_is_four_hundred_one_with_reason_invalid_reauth_token()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, _response.StatusCode);
            Assert.Equal("invalid_reauth_token", await IdentitySpecFixture.ReadProblemReasonAsync(_response));
        }
    }

    public class Given_a_caller_who_has_not_been_provisioned_yet(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_nine_with_reason_user_not_provisioned()
        {
            var uid = Guid.NewGuid().ToString();
            var reauthToken = fixture.CreateToken(uid, authTime: DateTimeOffset.UtcNow, signInProvider: "google.com");

            var response = await fixture.SendLinkProviderAsync(fixture.CreateToken(uid), reauthToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("user_not_provisioned", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    private static async Task<int> SeedActiveUserAsync(IdentitySpecFixture fixture, string uid, string email)
    {
        var user = User.Provision(uid, email, UserClassification.External, DateTimeOffset.UtcNow);
        fixture.DbContext.Users.Add(user);
        await fixture.DbContext.SaveChangesAsync();

        return user.Id;
    }
}
