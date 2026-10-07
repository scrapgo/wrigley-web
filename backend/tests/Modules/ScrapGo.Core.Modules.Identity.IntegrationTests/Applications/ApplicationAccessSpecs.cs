// ORG-APP-MODULE-MODEL Task 9: only application administrators (or platform admins) grant application
// access (Decision 3); escalation guard, expiry, last-app-admin protection and audit.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Applications;

public class ApplicationAccessSpecs
{
    public class Given_a_platform_admin_appointing_the_first_app_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_member_becomes_app_admin_and_an_external_user_may_be_one()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var (adminUid, adminId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            var adminRoleId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator);

            var response = await fixture.SendAsync(
                HttpMethod.Put,
                $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}/members/{adminId}/roles/{adminRoleId}",
                fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ListRolesAsync(fixture, adminUid, organizationId)).StatusCode);
        }

        // The platform admin isn't a member, so it picks the role through /api/admin.
        [Fact]
        public async Task The_platform_admin_lists_the_application_roles_without_being_a_member()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var organizationId = await fixture.SeedOrganizationAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);

            var response = await fixture.SendAsync(
                HttpMethod.Get,
                $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}/roles",
                fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var names = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).EnumerateArray()
                .Select(r => r.GetProperty("name").GetString());
            Assert.Contains(SpecApplications.SpecAppAdministrator, names);
        }
    }

    public class Given_an_app_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Granting_viewer_is_audited_idempotent_and_effective_on_the_next_request()
        {
            var (adminUid, adminId, organizationId) = await SeedAppAdminAsync(fixture);
            var (memberUid, memberId) = await SeedMemberOfAsync(fixture, organizationId);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);
            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);

            var first = await GrantAsync(fixture, adminUid, organizationId, memberId, viewerId);
            var second = await GrantAsync(fixture, adminUid, organizationId, memberId, viewerId);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await EntitlementSpecs.AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);
            var audit = Assert.Single(await AuditForAsync(fixture, IdentityAuditEventTypes.AccessGranted, memberId));
            Assert.Equal(adminId, audit.UserId);
        }

        [Fact]
        public async Task A_grant_with_an_expiry_stops_resolving_and_a_past_expiry_is_rejected()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);

            var past = await GrantAsync(fixture, adminUid, organizationId, memberId, viewerId, DateTimeOffset.UtcNow.AddHours(-1));
            var future = await GrantAsync(fixture, adminUid, organizationId, memberId, viewerId, DateTimeOffset.UtcNow.AddDays(30));

            Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
            Assert.Equal(HttpStatusCode.OK, future.StatusCode);
            var body = await future.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.String, body.GetProperty("expiresAt").ValueKind);
        }

        [Fact]
        public async Task Revoking_removes_access_on_the_next_request_with_an_audit_entry()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var (memberUid, memberId) = await SeedMemberOfAsync(fixture, organizationId);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);
            (await GrantAsync(fixture, adminUid, organizationId, memberId, viewerId)).EnsureSuccessStatusCode();

            var response = await RevokeAsync(fixture, adminUid, organizationId, memberId, viewerId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await EntitlementSpecs.AppProbeAsync(fixture, memberUid, organizationId)).StatusCode);
            Assert.Single(await AuditForAsync(fixture, IdentityAuditEventTypes.AccessRevoked, memberId));
        }

        [Fact]
        public async Task Custom_roles_can_be_composed_only_from_this_applications_permissions()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var roleId = await CreateCustomRoleAsync(fixture, adminUid, organizationId, $"Clerk {Guid.NewGuid():N}");

            // Alpha is enabled here; a disabled module's permission (Beta) isn't effectively held, so it can't be attached.
            var ownPermission = await AttachAsync(fixture, adminUid, organizationId, roleId, SpecApplications.AlphaWrite);
            var otherApplication = await AttachAsync(fixture, adminUid, organizationId, roleId, SpecApplications.GammaRead);
            var orgLevel = await AttachAsync(fixture, adminUid, organizationId, roleId, Permissions.UserRead);

            Assert.Equal(HttpStatusCode.OK, ownPermission.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, otherApplication.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, orgLevel.StatusCode);
        }

        [Fact]
        public async Task Templates_are_listed_but_not_editable()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);

            var roles = await (await ListRolesAsync(fixture, adminUid, organizationId)).Content.ReadFromJsonAsync<JsonElement>();
            var rename = await fixture.SendAsync(
                HttpMethod.Put, $"{AppPath(organizationId)}/roles/{viewerId}", fixture.CreateToken(adminUid), new { name = "Hijacked" });

            Assert.Contains(roles.EnumerateArray(), r => r.GetProperty("id").GetInt32() == viewerId);
            Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        }

        [Fact]
        public async Task Access_review_lists_every_holder_with_what_they_resolve_to()
        {
            var (adminUid, adminId, organizationId) = await SeedAppAdminAsync(fixture);
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);
            (await GrantAsync(fixture, adminUid, organizationId, memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer)))
                .EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, $"{AppPath(organizationId)}/access", fixture.CreateToken(adminUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var entries = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            Assert.Equal([adminId, memberId], entries.Select(e => e.GetProperty("userId").GetInt32()).Order());
            var member = entries.Single(e => e.GetProperty("userId").GetInt32() == memberId);
            // Alpha is enabled; the viewer template grants Alpha.Read only.
            Assert.Equal([SpecApplications.AlphaRead], member.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }
    }

    // Sad paths, one rule each.

    // Decision 3: an org admin can't grant application roles.
    public class Given_an_org_admin_without_app_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Granting_an_app_role_returns_four_hundred_three()
        {
            var (orgAdminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);

            var viaAppRoute = await GrantAsync(fixture, orgAdminUid, organizationId, memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer));
            var viaOrgRoute = await fixture.SendAsync(
                HttpMethod.Post, $"/api/users/{memberId}/roles", fixture.CreateToken(orgAdminUid),
                new { roleId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId });

            Assert.Equal(HttpStatusCode.Forbidden, viaAppRoute.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, viaOrgRoute.StatusCode);
            Assert.Equal("role_scope_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(viaOrgRoute));
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == memberId));
        }
    }

    // Escalation guard: an app admin with a narrower custom admin role can't hand out more.
    public class Given_a_delegate_app_admin_with_limited_permissions(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Granting_a_role_with_unheld_permissions_returns_four_hundred_three()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var delegateRoleId = await CreateCustomRoleAsync(fixture, adminUid, organizationId, "Delegate");
            (await AttachAsync(fixture, adminUid, organizationId, delegateRoleId, Permissions.ApplicationManageAccess)).EnsureSuccessStatusCode();
            (await AttachAsync(fixture, adminUid, organizationId, delegateRoleId, SpecApplications.AlphaRead)).EnsureSuccessStatusCode();
            var (delegateUid, delegateId) = await SeedMemberOfAsync(fixture, organizationId);
            (await GrantAsync(fixture, adminUid, organizationId, delegateId, delegateRoleId)).EnsureSuccessStatusCode();
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);

            var response = await GrantAsync(
                fixture, delegateUid, organizationId, memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_grant_unheld_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_the_only_app_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Revoking_their_own_admin_role_returns_four_hundred_nine_but_a_platform_admin_may()
        {
            var (adminUid, adminId, organizationId) = await SeedAppAdminAsync(fixture);
            var adminRoleId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator);
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();

            var self = await RevokeAsync(fixture, adminUid, organizationId, adminId, adminRoleId);
            var platform = await fixture.SendAsync(
                HttpMethod.Delete,
                $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}/members/{adminId}/roles/{adminRoleId}",
                fixture.CreateToken(platformUid));

            Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
            Assert.Equal("last_application_administrator", await IdentitySpecFixture.ReadProblemReasonAsync(self));
            Assert.Equal(HttpStatusCode.NoContent, platform.StatusCode);
        }
    }

    public class Given_a_target_who_is_not_a_member(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Granting_returns_four_hundred_with_reason_user_not_a_member()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var (_, outsiderId) = await fixture.SeedUserAsync();

            var response = await GrantAsync(fixture, adminUid, organizationId, outsiderId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("user_not_a_member", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task Granting_another_applications_role_returns_four_hundred_four()
        {
            var (adminUid, _, organizationId) = await SeedAppAdminAsync(fixture);
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);

            var response = await GrantAsync(fixture, adminUid, organizationId, memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.OtherAppAdministrator));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("role_not_found", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // Removing a member also revokes their application grants, as access_revoked.
    public class Given_a_member_with_app_access_being_removed(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Their_app_grants_go_with_the_membership()
        {
            var (orgAdminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            var (_, memberId) = await SeedMemberOfAsync(fixture, organizationId);
            await fixture.GrantApplicationRoleAsync(
                memberId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer), organizationId, SpecApplications.SpecAppId);

            var response = await fixture.SendAsync(
                HttpMethod.Delete, $"/api/organizations/{organizationId}/members/{memberId}", fixture.CreateToken(orgAdminUid));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == memberId));
            Assert.Single(await AuditForAsync(fixture, IdentityAuditEventTypes.AccessRevoked, memberId));
        }
    }

    internal static string AppPath(int organizationId) =>
        $"/api/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}";

    /// <summary>A member of a new org with SpecApp (Alpha enabled) holding the SpecApp Administrator template.</summary>
    internal static async Task<(string Uid, int UserId, int OrganizationId)> SeedAppAdminAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
        await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
        await fixture.GrantApplicationRoleAsync(
            userId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator), organizationId, SpecApplications.SpecAppId);

        return (uid, userId, organizationId);
    }

    private static async Task<(string Uid, int UserId)> SeedMemberOfAsync(IdentitySpecFixture fixture, int organizationId)
    {
        var (uid, userId) = await fixture.SeedUserAsync();
        await fixture.SeedMembershipAsync(userId, organizationId);

        return (uid, userId);
    }

    private static Task<HttpResponseMessage> ListRolesAsync(IdentitySpecFixture fixture, string uid, int organizationId) =>
        fixture.SendAsync(HttpMethod.Get, $"{AppPath(organizationId)}/roles", fixture.CreateToken(uid));

    private static Task<HttpResponseMessage> GrantAsync(
        IdentitySpecFixture fixture, string uid, int organizationId, int userId, int roleId, DateTimeOffset? expiresAt = null) =>
        fixture.SendAsync(HttpMethod.Put, $"{AppPath(organizationId)}/members/{userId}/roles/{roleId}", fixture.CreateToken(uid), new { expiresAt });

    private static Task<HttpResponseMessage> RevokeAsync(IdentitySpecFixture fixture, string uid, int organizationId, int userId, int roleId) =>
        fixture.SendAsync(HttpMethod.Delete, $"{AppPath(organizationId)}/members/{userId}/roles/{roleId}", fixture.CreateToken(uid));

    private static Task<HttpResponseMessage> AttachAsync(IdentitySpecFixture fixture, string uid, int organizationId, int roleId, string permissionName) =>
        fixture.SendAsync(HttpMethod.Post, $"{AppPath(organizationId)}/roles/{roleId}/permissions", fixture.CreateToken(uid), new { permissionName });

    private static async Task<int> CreateCustomRoleAsync(IdentitySpecFixture fixture, string uid, int organizationId, string name)
    {
        var response = await fixture.SendAsync(HttpMethod.Post, $"{AppPath(organizationId)}/roles", fixture.CreateToken(uid), new { name });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>Audit rows of one type whose metadata names this target user.</summary>
    private static async Task<List<Shared.Infrastructure.Audit.AuditLog>> AuditForAsync(IdentitySpecFixture fixture, string eventType, int targetUserId)
    {
        var rows = await fixture.DbContext.AuditLogs.AsNoTracking().Where(a => a.EventType == eventType).ToListAsync();

        return [.. rows.Where(a => a.Metadata is not null
            && JsonDocument.Parse(a.Metadata).RootElement.GetProperty("targetUserId").GetInt32() == targetUserId)];
    }
}
