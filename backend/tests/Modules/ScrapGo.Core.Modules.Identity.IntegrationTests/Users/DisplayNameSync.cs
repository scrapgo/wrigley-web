// POST /api/session stores the token's `name` claim, so a name set on the GCIP
// account (e.g. on the invitation sign-up form) reaches the user record.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class DisplayNameSync
{
    public class Given_an_existing_user_without_a_name(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_sign_in_whose_token_carries_a_name_stores_it()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"buyer-{Guid.NewGuid():N}@acme.example");

            var response = await StartSessionAsync(fixture, uid, name: "  Kim Lee  ");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Kim Lee", await DisplayNameOfAsync(fixture, userId));
        }

        [Fact]
        public async Task A_later_name_replaces_the_stored_one()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"buyer-{Guid.NewGuid():N}@acme.example");
            (await StartSessionAsync(fixture, uid, name: "Kim Lee")).EnsureSuccessStatusCode();

            (await StartSessionAsync(fixture, uid, name: "Kim Lee-Park")).EnsureSuccessStatusCode();

            Assert.Equal("Kim Lee-Park", await DisplayNameOfAsync(fixture, userId));
        }

        // A token without a name (e.g. an older session) never erases the stored one.
        [Fact]
        public async Task A_sign_in_without_a_name_keeps_the_stored_one()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"buyer-{Guid.NewGuid():N}@acme.example");
            (await StartSessionAsync(fixture, uid, name: "Kim Lee")).EnsureSuccessStatusCode();

            (await StartSessionAsync(fixture, uid, name: null)).EnsureSuccessStatusCode();

            Assert.Equal("Kim Lee", await DisplayNameOfAsync(fixture, userId));
        }
    }

    private static Task<HttpResponseMessage> StartSessionAsync(IdentitySpecFixture fixture, string uid, string? name) =>
        fixture.StartSessionAsync(fixture.CreateToken(
            uid,
            signInProvider: "password",
            emailVerified: true,
            extraClaims: name is null ? null : [("name", name)]));

    private static Task<string?> DisplayNameOfAsync(IdentitySpecFixture fixture, int userId) =>
        fixture.DbContext.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync();
}
