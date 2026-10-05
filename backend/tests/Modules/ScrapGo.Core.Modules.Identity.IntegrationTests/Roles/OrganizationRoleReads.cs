// ADMIN-API-STATUS Task 4: role reads under /api/organizations/{organizationId}/roles, gated on Role.Read.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class OrganizationRoleReads
{
    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_roles_lists_the_orgs_roles_and_the_platform_built_ins()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var customRoleId = await CustomRoleCrud.ReadIdAsync(
                await CustomRoleCrud.PostRoleAsync(fixture, uid, organizationId, "Dispatcher", null));

            var response = await fixture.SendAsync(HttpMethod.Get, RolesPath(organizationId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = await ReadRolesAsync(response);
            Assert.Contains(roles, r => r.Id == customRoleId && r.OrganizationId == organizationId);
            Assert.Contains(roles, r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null);
            Assert.Contains(roles, r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null);
        }

        [Fact]
        public async Task Get_role_returns_the_role_with_its_permission_names()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await CustomRoleCrud.ReadIdAsync(
                await CustomRoleCrud.PostRoleAsync(fixture, uid, organizationId, "Auditor", "Reads invoices"));
            (await fixture.SendAsync(HttpMethod.Post, $"/api/roles/{roleId}/permissions", fixture.CreateToken(uid),
                new { permissionName = Permissions.InvoiceRead })).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, $"{RolesPath(organizationId)}/{roleId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auditor", body.GetProperty("name").GetString());
            Assert.Equal(organizationId, body.GetProperty("organizationId").GetInt32());
            Assert.Equal([Permissions.InvoiceRead], body.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }

        [Fact]
        public async Task Get_role_permissions_returns_the_names_of_a_built_in()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var builtInId = await BuiltInRoleIdAsync(fixture, DefaultRoleNames.OrganizationAdministrator);

            var response = await fixture.SendAsync(
                HttpMethod.Get, $"{RolesPath(organizationId)}/{builtInId}/permissions", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var names = (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!
                .Select(p => p.GetProperty("name").GetString()!);
            Assert.Equal(Permissions.All.Where(p => p != Permissions.AdminAccess).Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
        }
    }

    // Permission-based, not role-based: any role carrying Role.Read is enough.
    public class Given_a_member_holding_role_read_through_a_custom_role(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_roles_is_allowed()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.RoleRead);

            var response = await fixture.SendAsync(HttpMethod.Get, RolesPath(organizationId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_a_member_without_role_read(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/1")]
        [InlineData("/1/permissions")]
        public async Task Every_role_read_returns_four_hundred_three(string suffix)
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.InvoiceRead);

            var response = await fixture.SendAsync(HttpMethod.Get, RolesPath(organizationId) + suffix, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // The membership guard runs first: holding Role.Read elsewhere is no way in.
    public class Given_an_admin_of_another_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_roles_for_an_org_they_are_not_a_member_of_returns_four_hundred_three()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var otherOrg = await fixture.SeedOrganizationAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, RolesPath(otherOrg), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("no_active_membership", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/1")]
        [InlineData("/1/permissions")]
        public async Task Every_role_read_returns_four_hundred_one(string suffix)
        {
            var organizationId = await fixture.SeedOrganizationAsync();

            var response = await fixture.Client.GetAsync(RolesPath(organizationId) + suffix);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    // 404, not 403: a role id from another organization is indistinguishable
    // from one that doesn't exist, so it can't be probed for.
    public class Given_a_role_belonging_to_another_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("")]
        [InlineData("/permissions")]
        public async Task Reading_it_through_my_org_returns_four_hundred_four(string suffix)
        {
            var (uidA, _, orgA) = await fixture.SeedOrganizationAdministratorAsync();
            var (uidB, _, orgB) = await fixture.SeedOrganizationAdministratorAsync();
            var orgBRoleId = await CustomRoleCrud.ReadIdAsync(await CustomRoleCrud.PostRoleAsync(fixture, uidB, orgB, "Org B Only", null));

            var response = await fixture.SendAsync(HttpMethod.Get, $"{RolesPath(orgA)}/{orgBRoleId}{suffix}", fixture.CreateToken(uidA));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task It_never_appears_in_my_orgs_list()
        {
            var (uidA, _, orgA) = await fixture.SeedOrganizationAdministratorAsync();
            var (uidB, _, orgB) = await fixture.SeedOrganizationAdministratorAsync();
            var orgBRoleId = await CustomRoleCrud.ReadIdAsync(await CustomRoleCrud.PostRoleAsync(fixture, uidB, orgB, "Hidden", null));

            var roles = await ReadRolesAsync(await fixture.SendAsync(HttpMethod.Get, RolesPath(orgA), fixture.CreateToken(uidA)));

            Assert.DoesNotContain(roles, r => r.Id == orgBRoleId);
            Assert.All(roles, r => Assert.True(r.OrganizationId is null || r.OrganizationId == orgA));
        }
    }

    public class Given_a_soft_deleted_or_unknown_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_deleted_role_returns_four_hundred_four()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await CustomRoleCrud.ReadIdAsync(await CustomRoleCrud.PostRoleAsync(fixture, uid, organizationId, "Short Lived", null));
            (await fixture.SendAsync(HttpMethod.Delete, $"/api/roles/{roleId}", fixture.CreateToken(uid))).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, $"{RolesPath(organizationId)}/{roleId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task An_unknown_role_returns_four_hundred_four()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"{RolesPath(organizationId)}/999999", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // Swagger is on in the Development environment the fixture runs.
    public class Given_the_swagger_document(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/organizations/{organizationId}/roles")]
        [InlineData("/api/organizations/{organizationId}/roles/{id}")]
        [InlineData("/api/organizations/{organizationId}/roles/{id}/permissions")]
        public async Task It_documents_each_role_read_as_a_get(string path)
        {
            var document = await fixture.Client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");

            Assert.True(document.GetProperty("paths").GetProperty(path).TryGetProperty("get", out _));
        }
    }

    private static string RolesPath(int organizationId) => $"/api/organizations/{organizationId}/roles";

    private static async Task<List<RoleRow>> ReadRolesAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<RoleRow>>())!;
    }

    private static Task<int> BuiltInRoleIdAsync(IdentitySpecFixture fixture, string name) =>
        fixture.DbContext.Roles.AsNoTracking()
            .Where(r => r.Name == name && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync();

    private sealed record RoleRow(int Id, string Name, int? OrganizationId);
}
