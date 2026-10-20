// ADMIN-API-STATUS Task 7: assign and revoke user roles, audited, effective on the next request.
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class UserRoleAssignment
{
    public class Given_a_platform_administrator_and_an_internal_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_a_role_returns_two_hundred_and_writes_one_row_and_one_audit_entry()
        {
            var (adminUid, adminId) = await SeedPlatformAdministratorAsync(fixture);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);

            var response = await AssignAsync(fixture, adminUid, targetId, roleId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await AssignmentCountAsync(fixture, targetId, roleId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleAssigned, targetId));
            Assert.Equal(adminId, audit.UserId);
            using var metadata = JsonDocument.Parse(audit.Metadata!);
            Assert.Equal(targetId, metadata.RootElement.GetProperty("targetUserId").GetInt32());
            Assert.Equal(roleId, metadata.RootElement.GetProperty("roleId").GetInt32());
        }

        [Fact]
        public async Task Assigning_the_same_role_twice_is_idempotent()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);
            (await AssignAsync(fixture, adminUid, targetId, roleId)).EnsureSuccessStatusCode();

            var response = await AssignAsync(fixture, adminUid, targetId, roleId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await AssignmentCountAsync(fixture, targetId, roleId));
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleAssigned, targetId));
        }

        [Fact]
        public async Task Revoking_returns_two_hundred_four_removes_the_row_and_writes_an_audit_entry()
        {
            var (adminUid, adminId) = await SeedPlatformAdministratorAsync(fixture);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);
            (await AssignAsync(fixture, adminUid, targetId, roleId)).EnsureSuccessStatusCode();

            var response = await RevokeAsync(fixture, adminUid, targetId, roleId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(0, await AssignmentCountAsync(fixture, targetId, roleId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.RoleRevoked, targetId));
            Assert.Equal(adminId, audit.UserId);
        }

        [Fact]
        public async Task Revoking_a_role_the_user_does_not_hold_is_idempotent()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);

            var response = await RevokeAsync(fixture, adminUid, targetId, roleId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await AuditAsync(fixture, IdentityAuditEventTypes.RoleRevoked, targetId));
        }

        // No stale permissions: the very next request sees the change, both ways.
        [Fact]
        public async Task The_change_takes_effect_on_the_users_next_request()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (targetUid, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);
            const string probe = "/api/users";

            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(targetUid))).StatusCode);

            (await AssignAsync(fixture, adminUid, targetId, roleId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(targetUid))).StatusCode);

            (await RevokeAsync(fixture, adminUid, targetId, roleId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.SendAsync(HttpMethod.Get, probe, fixture.CreateToken(targetUid))).StatusCode);
        }

        [Fact]
        public async Task Assigning_platform_administrator_grants_admin_access_on_the_next_request()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (newUid, newId) = await SeedInternalUserAsync(fixture);

            var response = await AssignAsync(fixture, adminUid, newId, await PlatformAdministratorRoleIdAsync(fixture));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await fixture.SendAsync(HttpMethod.Get, "/api/_test/platform-scoped", fixture.CreateToken(newUid))).StatusCode);
        }

        [Fact]
        public async Task Revoking_platform_administrator_from_one_of_two_holders_is_allowed()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, secondId) = await SeedPlatformAdministratorAsync(fixture);

            var response = await RevokeAsync(fixture, adminUid, secondId, await PlatformAdministratorRoleIdAsync(fixture));

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

            var response = await RevokeAsync(fixture, adminUid, adminId, roleId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_platform_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(1, await AssignmentCountAsync(fixture, adminId, roleId));
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

            var response = await RevokeAsync(fixture, adminUid, adminId, await PlatformAdministratorRoleIdAsync(fixture));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    // Role.Assign without Admin.Access is not enough: only platform administrators change roles.
    public class Given_a_user_holding_role_assign_but_not_admin_access(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assign_and_revoke_return_four_hundred_three_with_reason_platform_admin_required()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, Permissions.RoleAssign);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);

            var assign = await AssignAsync(fixture, uid, targetId, roleId);
            var revoke = await RevokeAsync(fixture, uid, targetId, roleId);

            Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
            Assert.Equal("platform_admin_required", await IdentitySpecFixture.ReadProblemReasonAsync(assign));
            Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
            Assert.Equal(0, await AssignmentCountAsync(fixture, targetId, roleId));
        }
    }

    // Escalation guard: nobody hands out a role richer than they are.
    public class Given_an_administrator_without_a_permission_the_role_grants(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_returns_four_hundred_three_with_reason_cannot_grant_unheld_permission()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            await fixture.GrantPermissionsAsync(userId, Permissions.AdminAccess, Permissions.RoleAssign);
            var (_, targetId) = await SeedInternalUserAsync(fixture);
            var roleId = await CreateRoleAsync(fixture, Permissions.UserRead);

            var response = await AssignAsync(fixture, uid, targetId, roleId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_grant_unheld_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(0, await AssignmentCountAsync(fixture, targetId, roleId));
        }
    }

    public class Given_an_unknown_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Assigning_returns_four_hundred_four_with_reason_role_not_found()
        {
            var (adminUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, targetId) = await SeedInternalUserAsync(fixture);

            var response = await AssignAsync(fixture, adminUid, targetId, roleId: 999999);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("role_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
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

    private static Task<HttpResponseMessage> AssignAsync(IdentitySpecFixture fixture, string actorUid, int userId, int roleId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/users/{userId}/roles", fixture.CreateToken(actorUid), new { roleId });

    private static Task<HttpResponseMessage> RevokeAsync(IdentitySpecFixture fixture, string actorUid, int userId, int roleId) =>
        fixture.SendAsync(HttpMethod.Delete, $"/api/users/{userId}/roles/{roleId}", fixture.CreateToken(actorUid));

    private static Task<(string Uid, int UserId)> SeedInternalUserAsync(IdentitySpecFixture fixture) =>
        fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserClassification.Internal);

    /// <summary>A role granting <paramref name="permissions"/>, assigned to nobody.</summary>
    private static async Task<int> CreateRoleAsync(IdentitySpecFixture fixture, params string[] permissions)
    {
        var role = Role.Create($"Role {Guid.NewGuid():N}", null, DateTimeOffset.UtcNow);
        fixture.DbContext.Roles.Add(role);
        await fixture.DbContext.SaveChangesAsync();

        var permissionIds = await fixture.DbContext.Permissions.Where(p => permissions.Contains(p.Name)).Select(p => p.Id).ToListAsync();
        fixture.DbContext.RolePermissions.AddRange(permissionIds.Select(id => RolePermission.Create(role.Id, id)));
        await fixture.DbContext.SaveChangesAsync();

        return role.Id;
    }

    private static Task<int> PlatformAdministratorRoleIdAsync(IdentitySpecFixture fixture) =>
        fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator)
            .Select(r => r.Id)
            .SingleAsync();

    private static async Task<(string Uid, int UserId)> SeedPlatformAdministratorAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        await fixture.AssignRoleAsync(userId, await PlatformAdministratorRoleIdAsync(fixture));

        return (uid, userId);
    }

    private static Task<int> AssignmentCountAsync(IdentitySpecFixture fixture, int userId, int roleId) =>
        fixture.DbContext.UserRoles.AsNoTracking().CountAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
