// ORG-APP-MODULE-MODEL Task 12: invitations pre-grant access by email; acceptance needs a verified, matching email.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Organizations;

public class InvitationSpecs
{
    public class Given_an_org_admin_inviting_with_an_org_level_pre_grant(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_invitee_accepting_with_a_verified_email_becomes_a_member_holding_the_role()
        {
            var (adminUid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var roleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Alpha);
            var email = $"invitee-{Guid.NewGuid():N}@acme.example";

            var created = await InviteAsync(fixture, adminUid, organizationId, email, new { roleId });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var token = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

            var inviteeUid = Guid.NewGuid().ToString();
            var accepted = await AcceptAsync(fixture, inviteeUid, email.ToUpperInvariant(), token, emailVerified: true);

            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var inviteeId = await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.IdentityPlatformUid == inviteeUid).Select(u => u.Id).SingleAsync();
            Assert.True(await fixture.DbContext.OrganizationMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == inviteeId && m.OrganizationId == organizationId && m.Status == MembershipStatus.Active));
            Assert.True(await fixture.DbContext.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.UserId == inviteeId && ur.RoleId == roleId && ur.OrganizationId == organizationId));
            Assert.Equal(HttpStatusCode.OK, (await fixture.SendAsync(
                HttpMethod.Get, $"/api/_test/permission-scoped/{organizationId}", fixture.CreateToken(inviteeUid))).StatusCode);
            var events = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId).Select(a => a.EventType).ToListAsync();
            Assert.Contains(IdentityAuditEventTypes.InvitationCreated, events);
            Assert.Contains(IdentityAuditEventTypes.InvitationAccepted, events);
            Assert.True(await fixture.DbContext.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EventType == IdentityAuditEventTypes.RoleAssigned && a.OrganizationId == organizationId && a.UserId == adminId));
        }

        [Fact]
        public async Task The_token_works_once()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var email = $"once-{Guid.NewGuid():N}@acme.example";
            var token = await InviteForTokenAsync(fixture, adminUid, organizationId, email);
            var inviteeUid = Guid.NewGuid().ToString();
            (await AcceptAsync(fixture, inviteeUid, email, token, emailVerified: true)).EnsureSuccessStatusCode();

            var replay = await AcceptAsync(fixture, inviteeUid, email, token, emailVerified: true);

            Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        }

        [Fact]
        public async Task Pending_invitations_are_listed_without_tokens_and_can_be_revoked()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var email = $"listed-{Guid.NewGuid():N}@acme.example";
            var created = await (await InviteAsync(fixture, adminUid, organizationId, email)).Content.ReadFromJsonAsync<JsonElement>();
            var invitationId = created.GetProperty("invitationId").GetInt32();
            var token = created.GetProperty("token").GetString()!;

            var list = await (await fixture.SendAsync(HttpMethod.Get, $"/api/organizations/{organizationId}/invitations", fixture.CreateToken(adminUid)))
                .Content.ReadAsStringAsync();
            var revoke = await fixture.SendAsync(
                HttpMethod.Delete, $"/api/organizations/{organizationId}/invitations/{invitationId}", fixture.CreateToken(adminUid));
            var accept = await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token, emailVerified: true);

            Assert.Contains(email, list);
            Assert.DoesNotContain(token, list);
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        }
    }

    public class Given_a_platform_admin_creating_an_org_for_someone_not_yet_signed_in(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_invitee_becomes_the_first_organization_administrator_on_acceptance()
        {
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            var email = $"founder-{Guid.NewGuid():N}@acme.example";

            var response = await fixture.SendAsync(HttpMethod.Post, "/api/admin/organizations", fixture.CreateToken(platformUid),
                new { name = $"Founded {Guid.NewGuid():N}", firstAdminEmail = email });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var organizationId = body.GetProperty("id").GetInt32();
            var token = body.GetProperty("invitation").GetProperty("token").GetString()!;

            var inviteeUid = Guid.NewGuid().ToString();
            (await AcceptAsync(fixture, inviteeUid, email, token, emailVerified: true)).EnsureSuccessStatusCode();

            var rename = await fixture.SendAsync(
                HttpMethod.Put, $"/api/organizations/{organizationId}", fixture.CreateToken(inviteeUid), new { name = "Renamed By Founder" });
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        }
    }

    public class Given_an_app_pre_grant_whose_application_was_removed_before_acceptance(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_pre_grant_is_skipped_and_the_membership_still_created()
        {
            // Inviting adds a member (an organization action, User.Update); pre-granting an
            // application role also needs Application.ManageAccess there: org admin and app admin.
            var (adminUid, adminId, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            await fixture.GrantApplicationRoleAsync(
                adminId, await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppAdministrator), organizationId, SpecApplications.SpecAppId);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);
            var email = $"stale-{Guid.NewGuid():N}@acme.example";
            var token = await InviteForTokenAsync(fixture, adminUid, organizationId, email, new { roleId = viewerId, applicationId = SpecApplications.SpecAppId });
            var (platformUid, _) = await fixture.SeedPlatformAdministratorAsync();
            (await fixture.SendAsync(HttpMethod.Delete, $"/api/admin/organizations/{organizationId}/applications/{SpecApplications.SpecAppId}",
                fixture.CreateToken(platformUid))).EnsureSuccessStatusCode();

            var accepted = await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token, emailVerified: true);

            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, body.GetProperty("grantedCount").GetInt32());
            Assert.Equal(1, body.GetProperty("skippedGrants").GetInt32());
        }
    }

    // Sad paths, one rule each.

    public class Given_an_invitee_whose_token_does_not_prove_the_email(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_unverified_email_returns_four_hundred_three_with_reason_email_not_verified()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var email = $"unverified-{Guid.NewGuid():N}@acme.example";
            var token = await InviteForTokenAsync(fixture, adminUid, organizationId, email);

            var response = await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token, emailVerified: false);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("email_not_verified", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_different_email_returns_four_hundred_three_with_reason_invitation_email_mismatch()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var token = await InviteForTokenAsync(fixture, adminUid, organizationId, $"right-{Guid.NewGuid():N}@acme.example");

            var response = await AcceptAsync(fixture, Guid.NewGuid().ToString(), "wrong@acme.example", token, emailVerified: true);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("invitation_email_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task An_expired_invitation_returns_four_hundred_ten()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var email = $"late-{Guid.NewGuid():N}@acme.example";
            var token = await InviteForTokenAsync(fixture, adminUid, organizationId, email);
            await fixture.DbContext.Database.ExecuteSqlAsync(
                $"UPDATE identity.invitations SET expires_at = now() - interval '1 minute' WHERE email_normalized = {email}");

            var response = await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token, emailVerified: true);

            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        }
    }

    public class Given_pre_grants_the_inviter_may_not_make(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_second_pending_invitation_for_the_same_email_returns_four_hundred_nine()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var email = $"twice-{Guid.NewGuid():N}@acme.example";
            (await InviteAsync(fixture, adminUid, organizationId, email)).EnsureSuccessStatusCode();

            var response = await InviteAsync(fixture, adminUid, organizationId, email);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("invitation_pending", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // Decision 3: an org admin can't pre-grant application roles either.
        [Fact]
        public async Task An_org_admin_pre_granting_an_app_role_returns_four_hundred_three()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            await fixture.EntitleAsync(organizationId, SpecApplications.SpecAppId, SpecApplications.AlphaModuleId);
            var viewerId = await fixture.ApplicationRoleIdAsync(SpecApplications.SpecAppViewer);

            var response = await InviteAsync(fixture, adminUid, organizationId, $"app-{Guid.NewGuid():N}@acme.example",
                new { roleId = viewerId, applicationId = SpecApplications.SpecAppId });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Pre_granting_platform_administrator_returns_four_hundred_four()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var platformAdminRoleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();

            var response = await InviteAsync(fixture, adminUid, organizationId, $"escalate-{Guid.NewGuid():N}@acme.example",
                new { roleId = platformAdminRoleId });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Escalation guard: an inviter can't pre-grant a role richer than themselves.
        [Fact]
        public async Task A_limited_inviter_pre_granting_a_richer_role_returns_four_hundred_three()
        {
            var (adminUid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (inviterUid, inviterId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(inviterId, organizationId);
            await fixture.GrantPermissionsAsync(inviterId, organizationId, Permissions.UserUpdate, Permissions.RoleAssign, SpecPermissions.Alpha);
            var richerRoleId = await CreateOrgRoleAsync(fixture, adminUid, organizationId, SpecPermissions.Beta);

            var response = await InviteAsync(fixture, inviterUid, organizationId, $"rich-{Guid.NewGuid():N}@acme.example", new { roleId = richerRoleId });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("cannot_grant_unheld_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_member_without_user_update_returns_four_hundred_three()
        {
            var (uid, userId, organizationId) = await fixture.SeedMemberAsync();
            await fixture.GrantPermissionsAsync(userId, organizationId, Permissions.UserRead);

            var response = await InviteAsync(fixture, uid, organizationId, $"nope-{Guid.NewGuid():N}@acme.example");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> InviteAsync(
        IdentitySpecFixture fixture, string uid, int organizationId, string email, params object[] grants) =>
        fixture.SendAsync(HttpMethod.Post, $"/api/organizations/{organizationId}/invitations", fixture.CreateToken(uid), new { email, grants });

    private static async Task<string> InviteForTokenAsync(
        IdentitySpecFixture fixture, string uid, int organizationId, string email, params object[] grants)
    {
        var response = await InviteAsync(fixture, uid, organizationId, email, grants);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static Task<HttpResponseMessage> AcceptAsync(
        IdentitySpecFixture fixture, string uid, string email, string token, bool emailVerified) =>
        fixture.SendAsync(
            HttpMethod.Post,
            "/api/invitations/accept",
            fixture.CreateToken(uid, email: email, extraClaims: [("email_verified", emailVerified ? "true" : "false")]),
            new { token });

    /// <summary>A custom org role, created and composed through the API by an org admin.</summary>
    private static async Task<int> CreateOrgRoleAsync(IdentitySpecFixture fixture, string adminUid, int organizationId, string permission)
    {
        var roleId = await Roles.CustomRoleCrud.ReadIdAsync(
            await Roles.CustomRoleCrud.PostRoleAsync(fixture, adminUid, organizationId, $"Role {Guid.NewGuid():N}", null));
        (await fixture.SendAsync(HttpMethod.Post, $"/api/roles/{roleId}/permissions", fixture.CreateToken(adminUid),
            new { permissionName = permission })).EnsureSuccessStatusCode();

        return roleId;
    }
}
