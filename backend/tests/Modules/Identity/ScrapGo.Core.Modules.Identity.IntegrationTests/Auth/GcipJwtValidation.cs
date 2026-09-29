// STORY-007: Validate GCIP JWTs on protected routes.
// Ported from identity-platform. The protected route is now GET /api/users/me
// (the legacy /api/_diag/whoami route was not carried over).

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Auth;

public class GcipJwtValidation
{
    public class Given_a_valid_gcip_jwt_for_the_expected_project_and_audience(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_protected_endpoint_call_returns_the_handlers_success_code()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_no_authorization_header(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_protected_endpoint_returns_four_hundred_one()
        {
            var response = await fixture.Client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class Given_an_expired_token(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var token = fixture.CreateToken(
                notBefore: DateTime.UtcNow.AddMinutes(-40),
                expires: DateTime.UtcNow.AddMinutes(-30));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class Given_a_token_with_a_tampered_signature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(tamperSignature: true));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class Given_a_token_with_the_wrong_audience(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(audience: "some-other-gcip-project"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class Given_a_token_with_the_wrong_issuer(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var response = await fixture.SendUsersMeAsync(
                fixture.CreateToken(issuer: "https://securetoken.google.com/some-other-gcip-project"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    // GcipTokenLifetimeValidator re-implements the ordinary window check it
    // replaces. A not-yet-valid token must still be rejected.
    public class Given_a_token_that_is_not_yet_valid(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var notBefore = DateTime.UtcNow.AddMinutes(10);
            var token = fixture.CreateToken(issuedAt: DateTime.UtcNow, notBefore: notBefore, expires: notBefore.AddMinutes(30));

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    // A nonsensical token must be a 401, not an unhandled exception surfacing as a 500.
    public class Given_a_token_whose_not_before_claim_is_after_its_expiry(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_response_is_four_hundred_one()
        {
            var expires = DateTime.UtcNow.AddMinutes(10);
            var token = fixture.CreateToken(issuedAt: DateTime.UtcNow, notBefore: expires.AddMinutes(5), expires: expires);

            var response = await fixture.SendUsersMeAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
