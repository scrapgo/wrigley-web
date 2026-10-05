// ADMIN-API-STATUS Task 9: rename an organization, add and remove members, under {organizationId}.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class OrganizationAdministration
{
    public class Given_an_org_admin_renaming_their_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Put_returns_the_updated_detail_keeps_the_slug_and_writes_an_audit_entry()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var slugBefore = await fixture.DbContext.Organizations.AsNoTracking()
                .Where(o => o.Id == organizationId).Select(o => o.Slug).SingleAsync();

            var response = await RenameAsync(fixture, uid, organizationId, "  Acme Metals Recycling  ");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Acme Metals Recycling", body.GetProperty("name").GetString());
            Assert.Equal(slugBefore, body.GetProperty("slug").GetString());
            var audit = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.OrganizationUpdated && a.OrganizationId == organizationId)
                .ToListAsync());
            Assert.Equal(adminId, audit.UserId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("--- !!")]
        public async Task An_invalid_name_returns_four_hundred_with_reason_invalid_name(string name)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await RenameAsync(fixture, uid, organizationId, name);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_name", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_an_org_admin_adding_a_member(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Post_adds_an_active_membership_grants_no_roles_and_writes_an_audit_entry()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, newUserId) = await fixture.SeedUserAsync();

            var response = await AddMemberAsync(fixture, uid, organizationId, newUserId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, newUserId, organizationId));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == newUserId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.MembershipAdded, newUserId));
            Assert.Equal(adminId, audit.UserId);
            Assert.Equal(organizationId, audit.OrganizationId);
        }

        [Fact]
        public async Task Adding_an_existing_member_is_idempotent()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, userId) = await fixture.SeedUserAsync();
            (await AddMemberAsync(fixture, uid, organizationId, userId)).EnsureSuccessStatusCode();

            var response = await AddMemberAsync(fixture, uid, organizationId, userId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(1, await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .CountAsync(m => m.UserId == userId && m.OrganizationId == organizationId));
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.MembershipAdded, userId));
        }

        [Fact]
        public async Task Adding_a_disabled_member_reactivates_their_membership()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, userId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(userId, organizationId, disabled: true);

            var response = await AddMemberAsync(fixture, uid, organizationId, userId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, userId, organizationId));
        }

        [Fact]
        public async Task An_unknown_user_returns_four_hundred_four()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await AddMemberAsync(fixture, uid, organizationId, 999_999)).StatusCode);
        }
    }

    public class Given_an_org_admin_removing_a_member(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Delete_removes_the_membership_and_only_this_orgs_roles_each_audited()
        {
            var (uid, adminId, orgA) = await fixture.SeedOrganizationAdministratorAsync();
            var (memberUid, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, orgA);
            var roleOne = await fixture.GrantPermissionsAsync(memberId, orgA, Permissions.InvoiceRead);
            var roleTwo = await fixture.GrantPermissionsAsync(memberId, orgA, Permissions.ReportRead);
            var orgB = await fixture.SeedOrganizationAsync();
            await fixture.SeedMembershipAsync(memberId, orgB);
            var orgBRole = await fixture.GrantPermissionsAsync(memberId, orgB, Permissions.InvoiceRead);

            var response = await RemoveMemberAsync(fixture, uid, orgA, memberId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Null(await MembershipStatusAsync(fixture, memberId, orgA));
            Assert.Equal([orgBRole], await fixture.DbContext.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == memberId).Select(ur => ur.RoleId).ToListAsync());
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, memberId, orgB));

            var revoked = await AuditAsync(fixture, IdentityAuditEventTypes.RoleRevoked, memberId);
            Assert.Equal([roleOne, roleTwo], revoked
                .Select(a => JsonDocument.Parse(a.Metadata!).RootElement.GetProperty("roleId").GetInt32()).Order());
            Assert.All(revoked, a => Assert.Equal(adminId, a.UserId));
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.MembershipRemoved, memberId));

            var probe = await fixture.SendAsync(HttpMethod.Get, $"/api/_test/permission-scoped/{orgA}", fixture.CreateToken(memberUid));
            Assert.Equal(HttpStatusCode.Forbidden, probe.StatusCode);
        }

        [Fact]
        public async Task Removing_a_non_member_is_idempotent()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, outsiderId) = await fixture.SeedUserAsync();

            var response = await RemoveMemberAsync(fixture, uid, organizationId, outsiderId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await AuditAsync(fixture, IdentityAuditEventTypes.MembershipRemoved, outsiderId));
        }

        [Fact]
        public async Task An_unknown_user_returns_four_hundred_four()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await RemoveMemberAsync(fixture, uid, organizationId, 999_999)).StatusCode);
        }

        [Fact]
        public async Task One_of_two_org_admins_may_be_removed()
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var secondAdminId = await AddSecondAdministratorAsync(fixture, organizationId);

            var response = await RemoveMemberAsync(fixture, uid, organizationId, secondAdminId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    // Sad paths, one rule each.

    // Lock-out protection: an organization always keeps one active OrganizationAdministrator.
    public class Given_the_only_org_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Removing_their_membership_returns_four_hundred_nine()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await RemoveMemberAsync(fixture, uid, organizationId, adminId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_organization_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, adminId, organizationId));
        }

        // The role-revocation route enforces the same rule.
        [Fact]
        public async Task Revoking_their_organization_administrator_role_returns_four_hundred_nine()
        {
            var (uid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Delete,
                $"/api/users/{adminId}/roles/{await OrganizationAdministratorRoleIdAsync(fixture)}?organizationId={organizationId}",
                fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_organization_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Cross-org: the membership guard keeps an admin of org A out of org B entirely.
    public class Given_an_admin_of_org_a_acting_on_org_b(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Rename_add_and_remove_are_stopped_by_the_membership_guard()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, memberOfB, orgB) = await fixture.SeedMemberAsync();
            var (_, outsiderId) = await fixture.SeedUserAsync();

            var responses = new[]
            {
                await RenameAsync(fixture, uid, orgB, "Hijacked"),
                await AddMemberAsync(fixture, uid, orgB, outsiderId),
                await RemoveMemberAsync(fixture, uid, orgB, memberOfB),
            };

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, memberOfB, orgB));
            Assert.Null(await MembershipStatusAsync(fixture, outsiderId, orgB));
        }

        // Through their own org's route, a member of B who isn't in A is just a non-member: nothing in B changes.
        [Fact]
        public async Task Removing_an_org_b_member_through_org_a_leaves_org_b_untouched()
        {
            var (uid, _, orgA) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, memberOfB, orgB) = await fixture.SeedMemberAsync();

            var response = await RemoveMemberAsync(fixture, uid, orgA, memberOfB);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, memberOfB, orgB));
        }
    }

    public class Given_a_member_without_the_permissions(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Rename_add_and_remove_return_four_hundred_three()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.UserRead);
            var (_, otherId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(otherId, organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, (await RenameAsync(fixture, uid, organizationId, "Renamed")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await AddMemberAsync(fixture, uid, organizationId, otherId)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await RemoveMemberAsync(fixture, uid, organizationId, otherId)).StatusCode);
            Assert.Equal(MembershipStatus.Active, await MembershipStatusAsync(fixture, otherId, organizationId));
        }

        // Permission-based: Organization.Update through a custom role is enough to rename.
        [Fact]
        public async Task Organization_update_through_a_custom_role_allows_renaming()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.OrganizationUpdate);

            Assert.Equal(HttpStatusCode.OK, (await RenameAsync(fixture, uid, organizationId, "Renamed By Delegate")).StatusCode);
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Rename_add_and_remove_return_four_hundred_one()
        {
            var organizationId = await fixture.SeedOrganizationAsync();

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await fixture.Client.PutAsJsonAsync($"/api/organizations/{organizationId}", new { name = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await fixture.Client.PostAsync($"/api/organizations/{organizationId}/members/1", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await fixture.Client.DeleteAsync($"/api/organizations/{organizationId}/members/1")).StatusCode);
        }
    }

    private static Task<HttpResponseMessage> RenameAsync(IdentitySpecFixture fixture, string uid, int organizationId, string name) =>
        fixture.SendAsync(HttpMethod.Put, $"/api/organizations/{organizationId}", fixture.CreateToken(uid), new { name });

    private static Task<HttpResponseMessage> AddMemberAsync(IdentitySpecFixture fixture, string uid, int organizationId, int userId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/organizations/{organizationId}/members/{userId}", fixture.CreateToken(uid));

    private static Task<HttpResponseMessage> RemoveMemberAsync(IdentitySpecFixture fixture, string uid, int organizationId, int userId) =>
        fixture.SendAsync(HttpMethod.Delete, $"/api/organizations/{organizationId}/members/{userId}", fixture.CreateToken(uid));

    private static async Task<MembershipStatus?> MembershipStatusAsync(IdentitySpecFixture fixture, int userId, int organizationId) =>
        await fixture.DbContext.OrganizationMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
            .Select(m => (MembershipStatus?)m.Status)
            .SingleOrDefaultAsync();

    private static Task<int> OrganizationAdministratorRoleIdAsync(IdentitySpecFixture fixture) =>
        fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync();

    private static async Task<int> AddSecondAdministratorAsync(IdentitySpecFixture fixture, int organizationId)
    {
        var (_, userId) = await fixture.SeedUserAsync();
        await fixture.SeedMembershipAsync(userId, organizationId);
        await fixture.AssignRoleAsync(userId, await OrganizationAdministratorRoleIdAsync(fixture), organizationId);

        return userId;
    }

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
