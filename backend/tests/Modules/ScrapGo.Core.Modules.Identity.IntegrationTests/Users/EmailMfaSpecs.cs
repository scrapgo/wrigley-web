// Email MFA for External users: with Identity:RequireMfaForExternalUsers on, each sign-in
// (the token's auth_time) needs a 6-digit code emailed to the user before any other route works.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class EmailMfaSpecs
{
    private const string SendPath = "/api/session/mfa/send";
    private const string VerifyPath = "/api/session/mfa/verify";

    public class Given_an_external_user_signs_in(MfaRequiredIdentitySpecFixture fixture) : IClassFixture<MfaRequiredIdentitySpecFixture>
    {
        [Fact]
        public async Task Every_other_route_needs_the_code_first()
        {
            var (token, _) = await SignInAsync(fixture);

            var me = await fixture.SendMeAsync(token);

            Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);
            Assert.Equal("mfa_required", await IdentitySpecFixture.ReadProblemReasonAsync(me));
        }

        [Fact]
        public async Task Sending_emails_a_code_and_returns_a_masked_address()
        {
            var (token, email) = await SignInAsync(fixture);

            var send = await fixture.SendAsync(HttpMethod.Post, SendPath, token);

            Assert.Equal(HttpStatusCode.OK, send.StatusCode);
            var body = await send.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal($"c***@{email.Split('@')[1]}", body.GetProperty("emailHint").GetString());
            Assert.Matches("^[0-9]{6}$", fixture.MfaCodes.LatestCodeFor(email));
        }

        [Fact]
        public async Task The_right_code_opens_every_route_for_this_sign_in()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();

            var verify = await VerifyAsync(fixture, token, fixture.MfaCodes.LatestCodeFor(email));

            Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await fixture.SendMeAsync(token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await fixture.StartSessionAsync(token)).StatusCode);
        }

        [Fact]
        public async Task A_refreshed_token_for_the_same_sign_in_stays_verified()
        {
            var authTime = DateTimeOffset.UtcNow.AddMinutes(-2);
            var (token, email, uid) = await SignInWithUidAsync(fixture, authTime);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            (await VerifyAsync(fixture, token, fixture.MfaCodes.LatestCodeFor(email))).EnsureSuccessStatusCode();

            var refreshed = fixture.CreateToken(uid, email: email, authTime: authTime, signInProvider: "password");

            Assert.Equal(HttpStatusCode.OK, (await fixture.SendMeAsync(refreshed)).StatusCode);
        }

        [Fact]
        public async Task Verifying_writes_audit_rows()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            (await VerifyAsync(fixture, token, fixture.MfaCodes.LatestCodeFor(email))).EnsureSuccessStatusCode();
            var userId = await fixture.DbContext.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();

            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(a => a.EventType == IdentityAuditEventTypes.MfaCodeSent && a.UserId == userId));
            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(a => a.EventType == IdentityAuditEventTypes.MfaVerified && a.UserId == userId));
        }

        [Fact]
        public async Task Only_a_salted_hash_of_the_code_is_stored()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            var code = fixture.MfaCodes.LatestCodeFor(email);
            var userId = await fixture.DbContext.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();

            var challenge = await fixture.DbContext.MfaChallenges.AsNoTracking().SingleAsync(c => c.UserId == userId);

            Assert.DoesNotContain(code, challenge.CodeHash);
            Assert.Equal(64, challenge.CodeHash.Length);
        }
    }

    public class Given_an_internal_user_signs_in(MfaRequiredIdentitySpecFixture fixture) : IClassFixture<MfaRequiredIdentitySpecFixture>
    {
        [Fact]
        public async Task They_are_never_asked_for_a_code()
        {
            var (uid, _) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var token = fixture.CreateToken(uid, authTime: DateTimeOffset.UtcNow);

            Assert.Equal(HttpStatusCode.OK, (await fixture.SendMeAsync(token)).StatusCode);
            var send = await fixture.SendAsync(HttpMethod.Post, SendPath, token);
            Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
            Assert.Equal("mfa_not_required", await IdentitySpecFixture.ReadProblemReasonAsync(send));
        }
    }

    // Sad paths, kept separate.

    public class Given_a_code_that_does_not_verify(MfaRequiredIdentitySpecFixture fixture) : IClassFixture<MfaRequiredIdentitySpecFixture>
    {
        [Fact]
        public async Task A_wrong_code_is_refused_and_counted()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();

            var verify = await VerifyAsync(fixture, token, WrongCode(fixture.MfaCodes.LatestCodeFor(email)));

            Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
            Assert.Equal("mfa_invalid_code", await IdentitySpecFixture.ReadProblemReasonAsync(verify));
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendMeAsync(token)).StatusCode);
        }

        [Fact]
        public async Task Five_wrong_codes_lock_it_even_against_the_right_one()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            var code = fixture.MfaCodes.LatestCodeFor(email);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                await VerifyAsync(fixture, token, WrongCode(code));
            }

            var verify = await VerifyAsync(fixture, token, code);
            Assert.Equal(HttpStatusCode.Conflict, verify.StatusCode);
            Assert.Equal("mfa_locked", await IdentitySpecFixture.ReadProblemReasonAsync(verify));
        }

        [Fact]
        public async Task An_expired_code_is_refused()
        {
            var (token, email) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            var userId = await fixture.DbContext.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            await fixture.DbContext.MfaChallenges.Where(c => c.UserId == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

            var verify = await VerifyAsync(fixture, token, fixture.MfaCodes.LatestCodeFor(email));

            Assert.Equal(HttpStatusCode.Conflict, verify.StatusCode);
            Assert.Equal("mfa_code_expired", await IdentitySpecFixture.ReadProblemReasonAsync(verify));
        }

        [Fact]
        public async Task A_new_sign_in_needs_a_new_code()
        {
            var (token, email, uid) = await SignInWithUidAsync(fixture, DateTimeOffset.UtcNow.AddMinutes(-10));
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();
            (await VerifyAsync(fixture, token, fixture.MfaCodes.LatestCodeFor(email))).EnsureSuccessStatusCode();

            var nextSignIn = fixture.CreateToken(uid, email: email, authTime: DateTimeOffset.UtcNow, signInProvider: "password");

            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendMeAsync(nextSignIn)).StatusCode);
        }

        [Fact]
        public async Task A_second_code_within_a_minute_is_refused()
        {
            var (token, _) = await SignInAsync(fixture);
            (await fixture.SendAsync(HttpMethod.Post, SendPath, token)).EnsureSuccessStatusCode();

            var again = await fixture.SendAsync(HttpMethod.Post, SendPath, token);

            Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
            Assert.Equal("mfa_resend_too_soon", await IdentitySpecFixture.ReadProblemReasonAsync(again));
            Assert.NotNull(again.Headers.RetryAfter);
        }

        [Fact]
        public async Task Verifying_before_any_code_was_sent_is_refused()
        {
            var (token, _) = await SignInAsync(fixture);

            var verify = await VerifyAsync(fixture, token, "123456");

            Assert.Equal(HttpStatusCode.Conflict, verify.StatusCode);
            Assert.Equal("mfa_no_code", await IdentitySpecFixture.ReadProblemReasonAsync(verify));
        }

        [Fact]
        public async Task A_token_without_auth_time_cannot_be_verified()
        {
            var email = $"customer-{Guid.NewGuid():N}@acme.example";
            var (uid, _) = await fixture.SeedUserAsync(email);

            var send = await fixture.SendAsync(HttpMethod.Post, SendPath, fixture.CreateToken(uid, email: email));

            Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
            Assert.Equal("mfa_invalid_session", await IdentitySpecFixture.ReadProblemReasonAsync(send));
        }
    }

    /// <summary>A seeded External user and a password sign-in token for them, with <c>auth_time</c>.</summary>
    private static async Task<(string Token, string Email)> SignInAsync(IdentitySpecFixture fixture)
    {
        var (token, email, _) = await SignInWithUidAsync(fixture, DateTimeOffset.UtcNow.AddMinutes(-1));
        return (token, email);
    }

    private static async Task<(string Token, string Email, string Uid)> SignInWithUidAsync(IdentitySpecFixture fixture, DateTimeOffset authTime)
    {
        var email = $"customer-{Guid.NewGuid():N}@acme.example";
        var (uid, _) = await fixture.SeedUserAsync(email);

        return (fixture.CreateToken(uid, email: email, authTime: authTime, signInProvider: "password"), email, uid);
    }

    private static Task<HttpResponseMessage> VerifyAsync(IdentitySpecFixture fixture, string token, string code) =>
        fixture.SendAsync(HttpMethod.Post, VerifyPath, token, new { code });

    /// <summary>A 6-digit code that differs from <paramref name="code"/>.</summary>
    private static string WrongCode(string code) => code == "000000" ? "111111" : "000000";
}
