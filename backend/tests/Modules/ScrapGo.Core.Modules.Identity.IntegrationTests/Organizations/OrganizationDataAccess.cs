// ADMIN-API-STATUS Task 3: organization and membership data access, isolated per organization.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;
using ScrapGo.Core.Modules.Identity.Application.Organizations;
using ScrapGo.Core.Shared.Kernel.Paging;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class OrganizationDataAccess
{
    public class Given_members_in_org_a_and_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Org_a_members_never_include_an_org_b_member()
        {
            var (_, memberA, orgA) = await fixture.SeedMemberAsync();
            var (_, memberB, _) = await fixture.SeedMemberAsync();

            var result = await ListMembersAsync(fixture, orgA, new PageRequest());

            Assert.Equal([memberA], result.Items.Select(m => m.UserId));
            Assert.DoesNotContain(result.Items, m => m.UserId == memberB);
        }

        [Fact]
        public async Task A_members_roles_are_only_those_held_in_that_org()
        {
            var (_, userId, orgA) = await fixture.SeedMemberAsync();
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgB);
            var orgARole = await fixture.GrantPermissionsAsync(userId, orgA, SpecPermissions.Alpha);
            await fixture.GrantPermissionsAsync(userId, orgB, SpecPermissions.Beta);
            await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserRead);

            var member = Assert.Single((await ListMembersAsync(fixture, orgA, new PageRequest())).Items);

            var role = Assert.Single(member.Roles);
            Assert.Equal(orgARole, role.RoleId);
            Assert.Equal(orgA, role.OrganizationId);
        }

        [Fact]
        public async Task Disabled_memberships_are_not_listed()
        {
            var orgId = await fixture.SeedOrganizationAsync();
            var (_, activeId) = await fixture.SeedUserAsync();
            var (_, disabledId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(activeId, orgId);
            await fixture.SeedMembershipAsync(disabledId, orgId, disabled: true);

            var result = await ListMembersAsync(fixture, orgId, new PageRequest());

            Assert.Equal([activeId], result.Items.Select(m => m.UserId));
        }
    }

    public class Given_more_members_than_one_page(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Members_page_by_user_id_with_the_total()
        {
            var orgId = await fixture.SeedOrganizationAsync();
            var userIds = new List<int>();
            for (var i = 0; i < 3; i++)
            {
                var (_, userId) = await fixture.SeedUserAsync();
                await fixture.SeedMembershipAsync(userId, orgId);
                userIds.Add(userId);
            }

            var second = await ListMembersAsync(fixture, orgId, new PageRequest(Page: 2, PageSize: 2));

            Assert.Equal(3, second.TotalCount);
            Assert.Equal([userIds[2]], second.Items.Select(m => m.UserId));
        }
    }

    public class Given_organizations_with_different_names_and_statuses(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Search_and_status_filter_the_platform_list()
        {
            var activeId = await fixture.SeedOrganizationAsync();
            var disabledId = await fixture.SeedOrganizationAsync(disabled: true);

            var disabledOnly = await ListOrganizationsAsync(
                fixture, new OrganizationListFilter(Status: OrganizationStatus.Disabled), new PageRequest());
            var activeName = await fixture.DbContext.Organizations.AsNoTracking()
                .Where(o => o.Id == activeId).Select(o => o.Name).SingleAsync();
            var byName = await ListOrganizationsAsync(
                fixture, new OrganizationListFilter(Search: activeName.ToUpperInvariant()), new PageRequest());

            Assert.Equal([disabledId], disabledOnly.Items.Select(o => o.Id));
            Assert.Equal([activeId], byName.Items.Select(o => o.Id));
        }
    }

    public class Given_a_membership_to_remove(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Remove_membership_inside_a_transaction_deletes_only_that_row()
        {
            var (_, userId, orgA) = await fixture.SeedMemberAsync();
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, orgB);

            await using var scope = fixture.Services.CreateAsyncScope();
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                organizations.RemoveMembership((await organizations.FindMembershipAsync(userId, orgA, ct))!);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, CancellationToken.None);

            var remaining = await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .Where(m => m.UserId == userId).Select(m => m.OrganizationId).ToListAsync();
            Assert.Equal([orgB], remaining);
        }

        [Fact]
        public async Task Get_by_id_returns_the_tracked_organization_or_null()
        {
            var orgId = await fixture.SeedOrganizationAsync();

            await using var scope = fixture.Services.CreateAsyncScope();
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            Assert.Equal(orgId, (await organizations.GetByIdAsync(orgId, CancellationToken.None))?.Id);
            Assert.Null(await organizations.GetByIdAsync(999_999, CancellationToken.None));
        }
    }

    private static async Task<PagedResult<OrganizationMemberDto>> ListMembersAsync(
        IdentitySpecFixture fixture, int organizationId, PageRequest page)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IAuthorizationQueries>()
            .ListMembersAsync(organizationId, page, CancellationToken.None);
    }

    private static async Task<PagedResult<OrganizationSummaryDto>> ListOrganizationsAsync(
        IdentitySpecFixture fixture, OrganizationListFilter filter, PageRequest page)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IAuthorizationQueries>()
            .ListOrganizationsAsync(filter, page, CancellationToken.None);
    }
}
