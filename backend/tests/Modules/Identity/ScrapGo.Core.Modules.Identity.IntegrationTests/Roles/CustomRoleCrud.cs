// STORY-016: Custom role CRUD, gated on OrganizationAdministrator of the role's organization.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class CustomRoleCrud
{
    public class Given_an_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_roles_creates_a_role_scoped_to_the_admins_org()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await PostRoleAsync(fixture, uid, organizationId, "Support Lead", "Handles support tickets");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var roleId = await ReadIdAsync(response);
            var role = await fixture.DbContext.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId);
            Assert.Equal(organizationId, role.OrganizationId);
            Assert.Equal("Support Lead", role.Name);
            Assert.Equal("Handles support tickets", role.Description);
        }

        [Fact]
        public async Task Put_roles_edits_name_and_description()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await ReadIdAsync(await PostRoleAsync(fixture, uid, organizationId, "Support Lead", "v1"));

            var response = await fixture.SendAsync(HttpMethod.Put, $"/api/roles/{roleId}", fixture.CreateToken(uid),
                new { name = "Support Lead II", description = "v2 description" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var role = await fixture.DbContext.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId);
            Assert.Equal("Support Lead II", role.Name);
            Assert.Equal("v2 description", role.Description);
        }

        [Fact]
        public async Task Delete_roles_soft_deletes_when_nothing_references_the_role()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await ReadIdAsync(await PostRoleAsync(fixture, uid, organizationId, "Temp Role", null));

            var response = await fixture.SendAsync(HttpMethod.Delete, $"/api/roles/{roleId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var role = await fixture.DbContext.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId);
            Assert.Equal(RoleStatus.Deleted, role.Status);
        }

        [Fact]
        public async Task Each_mutation_is_audit_logged_against_the_org()
        {
            var (uid, userId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await ReadIdAsync(await PostRoleAsync(fixture, uid, organizationId, "Audited Role", null));
            await fixture.SendAsync(HttpMethod.Put, $"/api/roles/{roleId}", fixture.CreateToken(uid), new { name = "Audited Role 2" });
            await fixture.SendAsync(HttpMethod.Delete, $"/api/roles/{roleId}", fixture.CreateToken(uid));

            var eventTypes = await fixture.DbContext.AuditLogs
                .Where(a => a.UserId == userId && a.OrganizationId == organizationId)
                .Select(a => a.EventType)
                .ToListAsync();

            Assert.Equal(
                [IdentityAuditEventTypes.RoleCreated, IdentityAuditEventTypes.RoleDeleted, IdentityAuditEventTypes.RoleUpdated],
                eventTypes.Order());
        }
    }

    // Sad paths, kept separate.

    public class Given_a_role_with_at_least_one_user_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Delete_returns_four_hundred_nine_with_reason_role_still_assigned()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await ReadIdAsync(await PostRoleAsync(fixture, uid, organizationId, "Referenced Role", null));
            var (_, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, organizationId);
            await fixture.AssignRoleAsync(memberId, roleId, organizationId);

            var response = await fixture.SendAsync(HttpMethod.Delete, $"/api/roles/{roleId}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("role_still_assigned", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_duplicate_role_name_in_the_same_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_returns_four_hundred_nine_with_reason_duplicate_role_name()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            (await PostRoleAsync(fixture, uid, organizationId, "Dispatcher", null)).EnsureSuccessStatusCode();

            var response = await PostRoleAsync(fixture, uid, organizationId, "Dispatcher", null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("duplicate_role_name", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_non_admin_member(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_roles_returns_four_hundred_three()
        {
            var (uid, _, organizationId) = await fixture.SeedMemberAsync();

            var response = await PostRoleAsync(fixture, uid, organizationId, "Should Not Exist", null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await fixture.DbContext.Roles.AnyAsync(r => r.Name == "Should Not Exist"));
        }
    }

    // The organization id in the request body is never trusted: being an admin
    // somewhere is not being an admin of the target organization.
    public class Given_an_admin_of_org_a_targeting_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Creating_a_role_in_org_b_returns_four_hundred_three()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var orgB = await fixture.SeedOrganizationAsync();

            var response = await PostRoleAsync(fixture, uid, orgB, "Planted Role", null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Editing_an_org_b_role_returns_four_hundred_three()
        {
            var (orgAAdminUid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var (orgBAdminUid, _, orgB) = await fixture.SeedOrganizationAdministratorAsync();
            var orgBRoleId = await ReadIdAsync(await PostRoleAsync(fixture, orgBAdminUid, orgB, "Org B Role", null));

            var response = await fixture.SendAsync(HttpMethod.Put, $"/api/roles/{orgBRoleId}", fixture.CreateToken(orgAAdminUid),
                new { name = "Hijacked" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Hardening over the legacy check: only the built-in, platform-defined
    // OrganizationAdministrator role confers admin rights. A custom role that
    // merely shares the name confers nothing.
    public class Given_a_member_holding_a_custom_role_named_organization_administrator(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_cannot_create_roles()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var lookalikeRoleId = await ReadIdAsync(
                await PostRoleAsync(fixture, adminUid, organizationId, DefaultRoleNames.OrganizationAdministrator, null));
            var (memberUid, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, organizationId);
            await fixture.AssignRoleAsync(memberId, lookalikeRoleId, organizationId);

            var response = await PostRoleAsync(fixture, memberUid, organizationId, "Escalated", null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_the_built_in_platform_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_org_admin_cannot_edit_it()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var builtInRoleId = await fixture.DbContext.Roles
                .Where(r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();

            var response = await fixture.SendAsync(HttpMethod.Put, $"/api/roles/{builtInRoleId}", fixture.CreateToken(uid),
                new { name = "Renamed" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    internal static Task<HttpResponseMessage> PostRoleAsync(
        IdentitySpecFixture fixture, string uid, int organizationId, string name, string? description) =>
        fixture.SendAsync(HttpMethod.Post, "/api/roles", fixture.CreateToken(uid), new { organizationId, name, description });

    internal static async Task<int> ReadIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
}
