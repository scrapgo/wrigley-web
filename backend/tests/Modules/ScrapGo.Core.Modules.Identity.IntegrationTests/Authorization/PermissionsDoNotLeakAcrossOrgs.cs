// STORY-020: A grant in one organization never applies in another.
namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class PermissionsDoNotLeakAcrossOrgs
{
    public class Given_a_member_of_both_orgs_holding_invoice_read_only_in_org_a(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private string _token = null!;
        private int _orgA;
        private int _orgB;

        public async Task InitializeAsync()
        {
            (var uid, var userId, _orgA) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, _orgA, SpecPermissions.Alpha);
            _orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, _orgB);
            _token = fixture.CreateToken(uid);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task The_org_a_request_is_allowed() =>
            Assert.Equal(HttpStatusCode.OK, (await RequirePermission.SendPermissionScopedAsync(fixture, _token, _orgA)).StatusCode);

        [Fact]
        public async Task The_org_b_request_returns_four_hundred_three() =>
            Assert.Equal(HttpStatusCode.Forbidden, (await RequirePermission.SendPermissionScopedAsync(fixture, _token, _orgB)).StatusCode);
    }

    // Without a membership in B, the membership guard denies before the permission is even evaluated.
    public class Given_a_caller_with_invoice_read_in_org_a_and_no_membership_in_org_b(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Requesting_invoice_read_in_org_b_returns_four_hundred_three()
        {
            var (uid, userId, orgA) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, orgA, SpecPermissions.Alpha);
            var orgB = await fixture.SeedOrganizationAsync();

            var response = await RequirePermission.SendPermissionScopedAsync(fixture, fixture.CreateToken(uid), orgB);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
