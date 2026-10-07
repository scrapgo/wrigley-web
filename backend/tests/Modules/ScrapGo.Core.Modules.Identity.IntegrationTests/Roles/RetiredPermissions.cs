// ORG-APP-MODULE-MODEL Task 2: the generic Invoice.* / Report.* permissions are retired (Decision 7).
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class RetiredPermissions
{
    public class Given_an_org_admin_composing_a_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData(Permissions.InvoiceRead)]
        [InlineData(Permissions.ReportExport)]
        public async Task Attaching_a_retired_permission_returns_four_hundred_with_reason_unknown_permission(string retired)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await CustomRoleCrud.ReadIdAsync(
                await CustomRoleCrud.PostRoleAsync(fixture, uid, organizationId, $"Role {Guid.NewGuid():N}", null));

            var response = await fixture.SendAsync(
                HttpMethod.Post, $"/api/roles/{roleId}/permissions", fixture.CreateToken(uid), new { permissionName = retired });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("unknown_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task The_built_in_organization_administrator_holds_none_of_them()
        {
            var names = await fixture.DbContext.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == 1)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

            Assert.DoesNotContain(names, Permissions.IsRetired);
        }
    }

    // A grant that slipped past retirement (seeded straight into the database)
    // still resolves to nothing.
    public class Given_a_stale_grant_of_a_retired_permission(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task It_never_resolves()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.InvoiceRead, SpecPermissions.Alpha);

            var response = await fixture.SendUsersMeAsync(fixture.CreateToken(uid, email: "stale@example.com"));
            response.EnsureSuccessStatusCode();

            var scope = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permissions").EnumerateArray());
            Assert.Equal([SpecPermissions.Alpha], scope.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }
    }
}
