// ADMIN-API-STATUS Task 3: role data access, isolated per organization.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Abstractions;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class RoleDataAccess
{
    public class Given_custom_roles_in_org_a_and_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Org_a_roles_are_its_own_plus_the_built_ins_never_org_b()
        {
            var (_, userA, orgA) = await fixture.SeedMemberAsync();
            var (_, userB, orgB) = await fixture.SeedMemberAsync();
            var orgARole = await fixture.GrantPermissionsAsync(userA, orgA, SpecPermissions.Alpha);
            var orgBRole = await fixture.GrantPermissionsAsync(userB, orgB, SpecPermissions.Alpha);

            await using var scope = fixture.Services.CreateAsyncScope();
            var roles = await scope.ServiceProvider.GetRequiredService<IAuthorizationQueries>()
                .ListRolesAsync(orgA, CancellationToken.None);

            Assert.Contains(roles, r => r.Id == orgARole && r.OrganizationId == orgA);
            Assert.DoesNotContain(roles, r => r.Id == orgBRole);
            Assert.Contains(roles, r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null);
            Assert.Contains(roles, r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null);
            Assert.All(roles, r => Assert.True(r.OrganizationId is null || r.OrganizationId == orgA));
        }

        [Fact]
        public async Task Soft_deleted_roles_are_not_listed()
        {
            var (_, userId, orgId) = await fixture.SeedMemberAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, orgId, SpecPermissions.Alpha);
            var role = await fixture.DbContext.Roles.SingleAsync(r => r.Id == roleId);
            role.MarkDeleted(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            await using var scope = fixture.Services.CreateAsyncScope();
            var roles = await scope.ServiceProvider.GetRequiredService<IAuthorizationQueries>()
                .ListRolesAsync(orgId, CancellationToken.None);

            Assert.DoesNotContain(roles, r => r.Id == roleId);
        }
    }

    public class Given_a_user_holding_roles_in_several_scopes(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task List_user_roles_returns_every_scope_platform_first()
        {
            var (_, userId, orgId) = await fixture.SeedMemberAsync();
            var orgRole = await fixture.GrantPermissionsAsync(userId, orgId, SpecPermissions.Alpha);
            var platformRole = await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserRead);

            await using var scope = fixture.Services.CreateAsyncScope();
            var assigned = await scope.ServiceProvider.GetRequiredService<IRoleRepository>()
                .ListUserRolesAsync(userId, CancellationToken.None);

            Assert.Equal([(platformRole, (int?)null), (orgRole, orgId)], assigned.Select(a => (a.RoleId, a.OrganizationId)));
        }

        [Fact]
        public async Task Get_role_permissions_returns_the_grants_by_name()
        {
            var (_, userId, orgId) = await fixture.SeedMemberAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, orgId, SpecPermissions.Beta, SpecPermissions.Alpha);

            await using var scope = fixture.Services.CreateAsyncScope();
            var names = await scope.ServiceProvider.GetRequiredService<IRoleRepository>()
                .GetRolePermissionsAsync(roleId, CancellationToken.None);

            Assert.Equal([SpecPermissions.Alpha, SpecPermissions.Beta], names);
        }

        [Fact]
        public async Task Remove_user_role_inside_a_transaction_revokes_only_that_scope()
        {
            var (_, userId, orgId) = await fixture.SeedMemberAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, orgId, SpecPermissions.Alpha);
            var otherOrg = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(userId, otherOrg);
            await fixture.AssignRoleAsync(userId, roleId, otherOrg);

            await using var scope = fixture.Services.CreateAsyncScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                Assert.Null(await roles.FindUserRoleAsync(userId, roleId, organizationId: null, ct));
                roles.RemoveUserRole((await roles.FindUserRoleAsync(userId, roleId, orgId, ct))!);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, CancellationToken.None);

            var remaining = await fixture.DbContext.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == userId && ur.RoleId == roleId).Select(ur => ur.OrganizationId).ToListAsync();
            Assert.Equal([(int?)otherOrg], remaining);
        }
    }
}
