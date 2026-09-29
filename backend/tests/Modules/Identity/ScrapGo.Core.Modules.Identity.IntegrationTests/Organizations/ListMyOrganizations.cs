// STORY-013: List my organizations.
using System.Net.Http.Json;
using ScrapGo.Core.Modules.Identity.Application.Organizations;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class ListMyOrganizations
{
    public class Given_a_caller_with_active_memberships_in_org_a_and_org_b(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_list_returns_exactly_those_two_orgs_in_id_order()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var orgA = await fixture.SeedOrganizationAsync();
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedOrganizationAsync(); // an organization the caller doesn't belong to
            await fixture.SeedMembershipAsync(userId, orgA);
            await fixture.SeedMembershipAsync(userId, orgB);

            var organizations = await ListAsync(fixture, uid);

            Assert.Equal([orgA, orgB], organizations.Select(o => o.Id));
            Assert.All(organizations, o => Assert.Equal(nameof(OrganizationStatus.Active), o.Status));
        }
    }

    // Exclusions, kept separate.

    public class Given_a_caller_with_a_disabled_membership_in_org_c(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Org_c_is_excluded_from_the_list()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var orgA = await fixture.SeedOrganizationAsync();
            var orgC = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgA);
            await fixture.SeedMembershipAsync(userId, orgC, disabled: true);

            var organizations = await ListAsync(fixture, uid);

            Assert.Equal([orgA], organizations.Select(o => o.Id));
        }
    }

    public class Given_an_organization_that_has_been_soft_deleted(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task That_organization_is_excluded_from_the_list()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var active = await fixture.SeedOrganizationAsync();
            var softDeleted = await fixture.SeedOrganizationAsync(disabled: true);
            await fixture.SeedMembershipAsync(userId, active);
            await fixture.SeedMembershipAsync(userId, softDeleted);

            var organizations = await ListAsync(fixture, uid);

            Assert.Equal([active], organizations.Select(o => o.Id));
        }
    }

    private static async Task<List<OrganizationSummaryDto>> ListAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendAsync(HttpMethod.Get, "/api/organizations", fixture.CreateToken(uid));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<OrganizationSummaryDto>>())!;
    }
}
