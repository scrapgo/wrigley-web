// STORY-008: Reject tokens with lifetime > 1 hour.
using ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authentication;

public class OneHourLifetimeCap
{
    public class Given_an_exactly_one_hour_lifetime_token(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_two_hundred()
        {
            var notBefore = DateTime.UtcNow.AddMinutes(-1);

            // exp - iat == 3600s, exactly at the cap ("exp - iat ≤ 3600").
            var token = fixture.CreateToken(issuedAt: notBefore, notBefore: notBefore, expires: notBefore.AddSeconds(3600));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Real GCIP tokens carry no nbf claim at all. The cap must still work on
    // that production shape.
    public class Given_a_token_with_no_not_before_claim_within_the_cap(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_two_hundred()
        {
            var issuedAt = DateTime.UtcNow;
            var token = fixture.CreateToken(includeNbf: false, issuedAt: issuedAt, expires: issuedAt.AddMinutes(30));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_two_hour_lifetime_token(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync()
        {
            // Otherwise entirely valid; exp - iat = 2 hours.
            var notBefore = DateTime.UtcNow.AddMinutes(-1);
            var token = fixture.CreateToken(issuedAt: notBefore, notBefore: notBefore, expires: notBefore.AddHours(2));

            _response = await fixture.SendUsersMeAsync(token);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void The_response_is_four_hundred_one() =>
            Assert.Equal(HttpStatusCode.Unauthorized, _response.StatusCode);

        [Fact]
        public async Task The_response_body_carries_the_token_lifetime_exceeded_reason() =>
            Assert.Equal(TokenLifetimeExceededException.Reason, await IdentitySpecFixture.ReadProblemReasonAsync(_response));

        // RFC 6750 §3: a 401 from a bearer-protected resource carries
        // WWW-Authenticate. HandleResponse() suppresses the handler's own
        // header, so this pins that OnChallenge sets it explicitly.
        [Fact]
        public void The_response_carries_a_www_authenticate_header() =>
            Assert.NotEmpty(_response.Headers.WwwAuthenticate);
    }

    // Pins the `>` (not `>=`) comparison: one second over is already rejected.
    public class Given_a_token_one_second_over_the_cap(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var notBefore = DateTime.UtcNow.AddMinutes(-1);
            var token = fixture.CreateToken(issuedAt: notBefore, notBefore: notBefore, expires: notBefore.AddSeconds(3601));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class Given_a_token_missing_the_iat_claim(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private HttpResponseMessage _response = null!;

        public async Task InitializeAsync() =>
            _response = await fixture.SendUsersMeAsync(fixture.CreateToken(includeIat: false));

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void The_response_is_four_hundred_one() =>
            Assert.Equal(HttpStatusCode.Unauthorized, _response.StatusCode);

        // "Can't verify the cap" is not "exceeds the cap": it must be the bare 401.
        [Fact]
        public async Task The_response_body_does_not_carry_the_token_lifetime_exceeded_reason() =>
            Assert.DoesNotContain(TokenLifetimeExceededException.Reason, await _response.Content.ReadAsStringAsync());
    }

    // An ordinary expiry within the cap must not be conflated with the cap failure.
    public class Given_an_ordinarily_expired_token_within_the_cap(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_body_does_not_carry_the_token_lifetime_exceeded_reason()
        {
            var token = fixture.CreateToken(notBefore: DateTime.UtcNow.AddMinutes(-40), expires: DateTime.UtcNow.AddMinutes(-30));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.DoesNotContain(TokenLifetimeExceededException.Reason, await response.Content.ReadAsStringAsync());
        }
    }

    // A bad signature never reaches the lifetime validator. OnChallenge must not misfire for it.
    public class Given_a_token_with_a_tampered_signature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_body_does_not_carry_the_token_lifetime_exceeded_reason()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(tamperSignature: true));

            Assert.DoesNotContain(TokenLifetimeExceededException.Reason, await response.Content.ReadAsStringAsync());
        }
    }
}
