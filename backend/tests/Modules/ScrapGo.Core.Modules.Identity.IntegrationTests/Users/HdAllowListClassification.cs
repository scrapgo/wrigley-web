// STORY-010: hd allow-list classifies internal users at provisioning.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class HdAllowListClassification
{
    public class Given_the_allow_list_contains_example_dot_com(AllowlistedHdIdentitySpecFixture fixture)
        : IClassFixture<AllowlistedHdIdentitySpecFixture>
    {
        [Fact]
        public async Task A_uid_with_hd_example_dot_com_is_provisioned_as_internal() =>
            Assert.Equal(UserClassification.Internal, await ProvisionAndReadClassificationAsync(fixture, hd: "example.com"));

        [Fact]
        public async Task The_allow_list_comparison_is_case_insensitive() =>
            Assert.Equal(UserClassification.Internal, await ProvisionAndReadClassificationAsync(fixture, hd: "EXAMPLE.COM"));

        [Fact]
        public async Task A_uid_with_hd_other_dot_com_is_provisioned_as_external() =>
            Assert.Equal(UserClassification.External, await ProvisionAndReadClassificationAsync(fixture, hd: "other.com"));

        [Fact]
        public async Task A_uid_with_no_hd_claim_is_provisioned_as_external() =>
            Assert.Equal(UserClassification.External, await ProvisionAndReadClassificationAsync(fixture, hd: null));
    }

    public class Given_a_user_already_provisioned_as_external(AllowlistedHdIdentitySpecFixture fixture)
        : IClassFixture<AllowlistedHdIdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();

        // First request has no hd claim, so the user is provisioned External.
        public async Task InitializeAsync() =>
            (await fixture.SendUsersMeAsync(fixture.CreateToken(_uid))).EnsureSuccessStatusCode();

        public Task DisposeAsync() => Task.CompletedTask;

        // Classification is decided at first provisioning only, never re-evaluated.
        [Fact]
        public async Task A_repeat_request_carrying_an_allow_listed_hd_claim_does_not_reclassify_the_user()
        {
            (await fixture.SendUsersMeAsync(fixture.CreateToken(_uid, hd: "example.com"))).EnsureSuccessStatusCode();

            var classification = await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.IdentityPlatformUid == _uid).Select(u => u.Classification).SingleAsync();

            Assert.Equal(UserClassification.External, classification);
        }
    }

    public class Given_no_allow_list_is_configured(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_uid_with_an_hd_claim_is_still_provisioned_as_external() =>
            Assert.Equal(UserClassification.External, await ProvisionAndReadClassificationAsync(fixture, hd: "example.com"));
    }

    private static async Task<UserClassification> ProvisionAndReadClassificationAsync(IdentitySpecFixture fixture, string? hd)
    {
        var uid = Guid.NewGuid().ToString();

        (await fixture.SendUsersMeAsync(fixture.CreateToken(uid, hd: hd))).EnsureSuccessStatusCode();

        return await fixture.DbContext.Users.AsNoTracking()
            .Where(u => u.IdentityPlatformUid == uid).Select(u => u.Classification).SingleAsync();
    }
}
