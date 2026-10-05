// ADMIN-API-STATUS Task 8: enable/disable users, platform-wide, audited, effective on the next request.
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class UserStatusChanges
{
    public class Given_a_platform_user_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Disabling_writes_an_audit_entry_and_the_gate_rejects_the_users_very_next_request()
        {
            var (actorUid, actorId) = await SeedUserAdministratorAsync(fixture);
            var (targetUid, targetId) = await fixture.SeedUserAsync();
            (await fixture.SendUsersMeAsync(fixture.CreateToken(targetUid))).EnsureSuccessStatusCode();

            var response = await DisableAsync(fixture, actorUid, targetId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(UserStatus.Disabled, await StatusOfAsync(fixture, targetId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.UserDisabled, targetId));
            Assert.Equal(actorId, audit.UserId);

            var next = await fixture.SendUsersMeAsync(fixture.CreateToken(targetUid));
            Assert.Equal(HttpStatusCode.Forbidden, next.StatusCode);
            Assert.Equal("user_disabled", await IdentitySpecFixture.ReadProblemReasonAsync(next));
        }

        [Fact]
        public async Task Enabling_restores_access_on_the_next_request_and_writes_an_audit_entry()
        {
            var (actorUid, actorId) = await SeedUserAdministratorAsync(fixture);
            var (targetUid, targetId) = await fixture.SeedUserAsync();
            (await DisableAsync(fixture, actorUid, targetId)).EnsureSuccessStatusCode();

            var response = await EnableAsync(fixture, actorUid, targetId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(UserStatus.Active, await StatusOfAsync(fixture, targetId));
            var audit = Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.UserEnabled, targetId));
            Assert.Equal(actorId, audit.UserId);
            Assert.Equal(HttpStatusCode.OK, (await fixture.SendUsersMeAsync(fixture.CreateToken(targetUid))).StatusCode);
        }

        [Fact]
        public async Task Disabling_twice_is_idempotent_with_one_audit_entry()
        {
            var (actorUid, _) = await SeedUserAdministratorAsync(fixture);
            var (_, targetId) = await fixture.SeedUserAsync();
            (await DisableAsync(fixture, actorUid, targetId)).EnsureSuccessStatusCode();

            var response = await DisableAsync(fixture, actorUid, targetId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Single(await AuditAsync(fixture, IdentityAuditEventTypes.UserDisabled, targetId));
        }

        [Fact]
        public async Task Enabling_an_active_user_is_idempotent_with_no_audit_entry()
        {
            var (actorUid, _) = await SeedUserAdministratorAsync(fixture);
            var (_, targetId) = await fixture.SeedUserAsync();

            var response = await EnableAsync(fixture, actorUid, targetId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await AuditAsync(fixture, IdentityAuditEventTypes.UserEnabled, targetId));
        }

        [Fact]
        public async Task An_unknown_user_returns_four_hundred_four()
        {
            var (actorUid, _) = await SeedUserAdministratorAsync(fixture);

            Assert.Equal(HttpStatusCode.NotFound, (await DisableAsync(fixture, actorUid, 999_999)).StatusCode);
        }
    }

    public class Given_two_active_platform_administrators(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task One_may_disable_the_other()
        {
            var (firstUid, _) = await SeedPlatformAdministratorAsync(fixture);
            var (_, secondId) = await SeedPlatformAdministratorAsync(fixture);

            var response = await DisableAsync(fixture, firstUid, secondId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    // Sad paths, one rule each.

    public class Given_a_caller_disabling_themselves(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task It_returns_four_hundred_three_with_reason_cannot_disable_self()
        {
            var (actorUid, actorId) = await SeedUserAdministratorAsync(fixture);

            var response = await DisableAsync(fixture, actorUid, actorId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_disable_self", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(UserStatus.Active, await StatusOfAsync(fixture, actorId));
        }
    }

    // Lock-out protection: disabled holders don't count as administrators.
    public class Given_the_last_enabled_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Disabling_them_returns_four_hundred_nine_even_if_a_disabled_one_remains()
        {
            var (actorUid, _) = await SeedUserAdministratorAsync(fixture);
            var (_, lastActiveId) = await SeedPlatformAdministratorAsync(fixture);
            var (_, disabledAdminId) = await SeedPlatformAdministratorAsync(fixture);
            var disabledAdmin = await fixture.DbContext.Users.SingleAsync(u => u.Id == disabledAdminId);
            disabledAdmin.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await DisableAsync(fixture, actorUid, lastActiveId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("last_platform_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.Equal(UserStatus.Active, await StatusOfAsync(fixture, lastActiveId));
        }
    }

    public class Given_a_caller_without_platform_user_update(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_user_with_no_roles_gets_four_hundred_three()
        {
            var (uid, _) = await fixture.SeedUserAsync();
            var (_, targetId) = await fixture.SeedUserAsync();

            Assert.Equal(HttpStatusCode.Forbidden, (await DisableAsync(fixture, uid, targetId)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await EnableAsync(fixture, uid, targetId)).StatusCode);
            Assert.Equal(UserStatus.Active, await StatusOfAsync(fixture, targetId));
        }

        // User.Update inside an organization (every org admin has it) is not platform-wide.
        [Fact]
        public async Task An_org_admin_gets_four_hundred_three_even_for_a_member_of_their_org()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, memberId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(memberId, organizationId);

            Assert.Equal(HttpStatusCode.Forbidden, (await DisableAsync(fixture, adminUid, memberId)).StatusCode);
            Assert.Equal(UserStatus.Active, await StatusOfAsync(fixture, memberId));
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("disable")]
        [InlineData("enable")]
        public async Task It_returns_four_hundred_one(string action)
        {
            var response = await fixture.Client.PostAsync($"/api/users/1/{action}", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> DisableAsync(IdentitySpecFixture fixture, string actorUid, int userId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/users/{userId}/disable", fixture.CreateToken(actorUid));

    private static Task<HttpResponseMessage> EnableAsync(IdentitySpecFixture fixture, string actorUid, int userId) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/users/{userId}/enable", fixture.CreateToken(actorUid));

    /// <summary>A user holding User.Update at platform scope through a custom platform role, not PlatformAdministrator.</summary>
    private static async Task<(string Uid, int UserId)> SeedUserAdministratorAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserUpdate);

        return (uid, userId);
    }

    private static async Task<(string Uid, int UserId)> SeedPlatformAdministratorAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        var roleId = await fixture.DbContext.Roles
            .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
            .Select(r => r.Id)
            .SingleAsync();
        await fixture.AssignRoleAsync(userId, roleId, organizationId: null);

        return (uid, userId);
    }

    private static Task<UserStatus> StatusOfAsync(IdentitySpecFixture fixture, int userId) =>
        fixture.DbContext.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Status).SingleAsync();

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
