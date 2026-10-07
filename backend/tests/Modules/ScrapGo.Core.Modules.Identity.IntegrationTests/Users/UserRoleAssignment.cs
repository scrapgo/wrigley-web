// ADMIN-API-STATUS Task 7: assign and revoke user roles, per scope, audited, effective on the next request.
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class UserRoleAssignment
{
    public class Given_an_org_admin_and_a_member_of_their_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_an_org_role_returns_two_hundred_and_writes_one_row_and_one_audit_entry()
        {
            var (adminUid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);

            var response = await AssignAsync(fixture, adminUid, memberId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await AssignmentCountAsync(fixture, memberId, roleId, organizationId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleAssigned, memberId));
            Assert.Equal(adminId, audit.UserId);
            Assert.Equal(organizationId, audit.OrganizationId);
            using var metadata = JsonDocument.Parse(audit.Metadata!);
            Assert.Equal(memberId, metadata.RootElement.GetProperty("targetUserId").GetInt32());
            Assert.Equal(roleId, metadata.RootElement.GetProperty("roleId").GetInt32());
            Assert.Equal(organizationId, metadata.RootElement.GetProperty("organizationId").GetInt32());
        }

        [Fact]
        public async Task Assigning_the_same_role_twice_is_idempotent()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);
            (await AssignAsync(fixture, adminUid, memberId, roleId, organizationId)).EnsureSuccessStatusCode();

            var response = await AssignAsync(fixture, adminUid, memberId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await AssignmentCountAsync(fixture, memberId, roleId, organizationId));
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleAssigned, memberId));
        }

        [Fact]
        public async Task Revoking_returns_two_hundred_four_removes_the_row_and_writes_an_audit_entry()
        {
            var (adminUid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);
            (await AssignAsync(fixture, adminUid, memberId, roleId, organizationId)).EnsureSuccessStatusCode();

            var response = await RevokeAsync(fixture, adminUid, memberId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(0, await AssignmentCountAsync(fixture, memberId, roleId, organizationId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleRevoked, memberId));
            Assert.Equal(adminId, audit.UserId);
        }

        [Fact]
        public async Task Revoking_a_role_the_user_does_not_hold_is_idempotent()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);

            var response = await RevokeAsync(fixture, adminUid, memberId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await AuditAsync(fixture, IdentityAuditEventTypes.RoleRevoked, memberId));
        }

        // No stale permissions: the very next request sees the change, both ways.
        [Fact]
        public async Task The_change_takes_effect_on_the_users_next_request()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (memberUid, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);
            var probe = $"/api/_test/permission-scoped/{organizationId}";

            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(memberUid))).StatusCode);

            (await AssignAsync(fixture, adminUid, memberId, roleId, organizationId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(memberUid))).StatusCode);

            (await RevokeAsync(fixture, adminUid, memberId, roleId, organizationId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(memberUid))).StatusCode);
        }
    }

    public class Given_a_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_platform_administrator_at_platform_scope_grants_admin_access_on_the_next_request()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (newUid, newId) = await fixture.SeedUserAsync("new-platform-admin@scrapgo.example", UserClassification.Internal);
            var platformAdminRoleId = await PlatformAdministratorRoleIdAsync(fixture);

            var response = await AssignAsync(fixture, adminUid, newId, platformAdminRoleId, organizationId: null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(newUid))).StatusCode);
        }

        [Fact]
        public async Task Revoking_platform_administrator_from_one_of_two_holders_is_allowed()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, secondId) = await SeedPlatformAdministratorAsync(fixture);

            var response = await RevokeAsync(fixture, adminUid, secondId, await PlatformAdministratorRoleIdAsync(fixture), organizationId: null);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    // Sad paths, one rule each.

    // Lock-out protection: there is always at least one platform administrator.
    public class Given_the_only_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Revoking_their_platform_administrator_role_returns_four_hundred_nine()
        {
            var (adminUid, adminId) = await SeedPlatformAdministratorAsync(fixture);
            var roleId = await PlatformAdministratorRoleIdAsync(fixture);

            var response = await RevokeAsync(fixture, adminUid, adminId, roleId, organizationId: null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_platform_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(1, await AssignmentCountAsync(fixture, adminId, roleId, organizationId: null));
        }
    }

    // A disabled holder can't act, so it doesn't count as a remaining administrator.
    public class Given_the_only_active_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Revoking_is_still_blocked_when_the_only_other_holder_is_disabled()
        {
            var (adminUid, adminId) = await SeedPlatformAdministratorAsync(fixture);
            var (_, disabledId) = await SeedPlatformAdministratorAsync(fixture);
            var disabled = await fixture.DbContext.Users.SingleAsync(u => u.Id == disabledId);
            disabled.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await RevokeAsync(fixture, adminUid, adminId, await PlatformAdministratorRoleIdAsync(fixture), organizationId: null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    public class Given_a_role_from_another_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_it_in_my_org_returns_four_hundred_four_and_writes_nothing()
        {
            var (adminAUid, _, orgA) = await fixture.SeedOrganizationAdministratorAsync();
            var (adminBUid, _, orgB) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, orgA);
            var orgBRoleId = await CreateOrgRoleAsync(fixture, adminBUid, orgB, SpecPermissions.Alpha);

            var response = await AssignAsync(fixture, adminAUid, memberId, orgBRoleId, orgA);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("role_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == memberId && ur.RoleId == orgBRoleId));
        }
    }

    public class Given_a_target_user_who_is_not_a_member_of_the_org(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_returns_four_hundred_with_reason_user_not_a_member()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, outsiderId) = await fixture.SeedUserAsync();
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);

            var response = await AssignAsync(fixture, adminUid, outsiderId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("user_not_a_member", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Org admins can never act at platform scope.
    public class Given_an_org_admin_targeting_platform_scope(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_a_platform_role_returns_four_hundred_three_with_reason_platform_admin_required()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);

            var response = await AssignAsync(fixture, adminUid, memberId, await PlatformAdministratorRoleIdAsync(fixture), organizationId: null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("platform_admin_required", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == memberId && ur.OrganizationId == null));
        }

        [Fact]
        public async Task Revoking_at_platform_scope_returns_four_hundred_three_with_reason_platform_admin_required()
        {
            var (adminUid, _, _) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, platformAdminId) = await SeedPlatformAdministratorAsync(fixture);

            var response = await RevokeAsync(fixture, adminUid, platformAdminId, await PlatformAdministratorRoleIdAsync(fixture), organizationId: null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("platform_admin_required", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Assigning_platform_administrator_inside_their_org_returns_four_hundred_with_reason_role_scope_mismatch()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var memberId = await SeedMemberOfAsync(fixture, organizationId);

            var response = await AssignAsync(fixture, adminUid, memberId, await PlatformAdministratorRoleIdAsync(fixture), organizationId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("role_scope_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Escalation guard: Role.Assign alone can't hand out a role richer than the caller.
    public class Given_a_member_holding_only_role_assign(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_organization_administrator_returns_four_hundred_three_with_reason_cannot_grant_unheld_permission()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.RoleAssign);
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var orgAdminRoleId = await fixture.DbContext.Roles
                .Where(r => r.Name == DefaultRoleNames.OrganizationAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();

            var response = await AssignAsync(fixture, uid, memberId, orgAdminRoleId, organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_grant_unheld_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_member_without_role_assign(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assign_and_revoke_return_four_hundred_three_with_reason_missing_permission()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(userId, organizationId);
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.RoleRead);
            var memberId = await SeedMemberOfAsync(fixture, organizationId);
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);

            var assign = await AssignAsync(fixture, uid, memberId, roleId, organizationId);
            var revoke = await RevokeAsync(fixture, uid, memberId, roleId, organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(assign));
            Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
            Assert.Equal(0, await AssignmentCountAsync(fixture, memberId, roleId, organizationId));
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assign_and_revoke_return_four_hundred_one()
        {
            var assign = await fixture.Client.PostAsync("/api/users/1/roles", System.Net.Http.Json.JsonContent.Create(new { roleId = 1 }));
            var revoke = await fixture.Client.DeleteAsync("/api/users/1/roles/1");

            Assert.Equal(HttpStatusCode.Unauthorized, assign.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, revoke.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> AssignAsync(
        IdentitySpecFixture fixture, string actorUid, int userId, int roleId, int? organizationId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/users/{userId}/roles", fixture.CreateToken(actorUid), new { roleId, organizationId });

    private static Task<HttpResponseMessage> RevokeAsync(
        IdentitySpecFixture fixture, string actorUid, int userId, int roleId, int? organizationId) =>
        fixture.SendAsync(
            HttpMethod.Delete,
            organizationId is { } id ? $"/api/users/{userId}/roles/{roleId}?organizationId={id}" : $"/api/users/{userId}/roles/{roleId}",
            fixture.CreateToken(actorUid));

    private static async Task<int> SeedMemberOfAsync(IdentitySpecFixture fixture, int organizationId)
    {
        var (_, userId) = await fixture.SeedUserAsync();
        await fixture.SeedMembershipAsync(userId, organizationId);

        return userId;
    }

    /// <summary>A custom org role created and composed through the API, as an org admin would.</summary>
    private static async Task<int> CreateOrgRoleAsync(IdentitySpecFixture fixture, string adminUid, int organizationId, string permission)
    {
        var roleId = await Roles.CustomRoleCrud.ReadIdAsync(
            await Roles.CustomRoleCrud.PostRoleAsync(fixture, adminUid, organizationId, $"Role {Guid.NewGuid():N}", null));
        (await fixture.SendAsync(HttpMethod.Post, $"/api/roles/{roleId}/permissions", fixture.CreateToken(adminUid),
            new { permissionName = permission })).EnsureSuccessStatusCode();

        return roleId;
    }

    private static Task<int> PlatformAdministratorRoleIdAsync(IdentitySpecFixture fixture) =>
        fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync();

    private static async Task<(string Uid, int UserId)> SeedPlatformAdministratorAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        await fixture.AssignRoleAsync(userId, await PlatformAdministratorRoleIdAsync(fixture), organizationId: null);

        return (uid, userId);
    }

    private static Task<int> AssignmentCountAsync(IdentitySpecFixture fixture, int userId, int roleId, int? organizationId) =>
        organizationId is { } id
            ? fixture.DbContext.UserRoles.AsNoTracking().CountAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.OrganizationId == id)
            : fixture.DbContext.UserRoles.AsNoTracking().CountAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.OrganizationId == null);

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
