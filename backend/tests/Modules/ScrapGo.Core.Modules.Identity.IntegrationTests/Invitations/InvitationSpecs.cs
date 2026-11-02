// B11: invitations (ACCESS-MODEL.md sections 7 and 9) and their acceptance through POST /api/session.
// External invitations are accepted with the token; internal ones at the invitee's first Google sign-in.
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Invitations;

public class InvitationSpecs
{
    private const string InvitationsPath = "/api/admin/invitations";

    public class Given_a_scrapgo_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_can_invite_and_get_a_copyable_accept_link()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();

            var response = await CreateAsync(fixture, inviterUid, email, [await ExternalRoleIdAsync(fixture)], [17511, 9583]);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var acceptLink = body.GetProperty("acceptLink").GetString()!;
            Assert.StartsWith($"{IdentitySpecFixture.FrontendBaseUrl}/invite/accept?token=", acceptLink);
            var invitation = body.GetProperty("invitation");
            Assert.Equal("Pending", invitation.GetProperty("status").GetString());
            Assert.Equal("External", invitation.GetProperty("userType").GetString());
            Assert.Equal([DefaultRoleNames.User], invitation.GetProperty("roleKeys").EnumerateArray().Select(r => r.GetString()));
            Assert.Equal([9583, 17511], invitation.GetProperty("supplierRecordIds").EnumerateArray().Select(i => i.GetInt32()));
            var expiresIn = invitation.GetProperty("expiresAt").GetDateTimeOffset() - DateTimeOffset.UtcNow;
            Assert.InRange(expiresIn, TimeSpan.FromDays(7) - TimeSpan.FromMinutes(5), TimeSpan.FromDays(7));
        }

        [Fact]
        public async Task Only_the_tokens_sha256_is_stored()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();

            var (id, token) = await CreateOkAsync(fixture, inviterUid, email);

            var stored = await fixture.DbContext.Invitations.AsNoTracking().SingleAsync(i => i.Id == id);
            Assert.Equal(32, Base64UrlDecodedLength(token));
            Assert.NotEqual(token, stored.TokenHash);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token))), stored.TokenHash);
        }

        [Fact]
        public async Task Creating_writes_an_invitation_created_audit_row()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var inviterId = await UserIdOfAsync(fixture, inviterUid);

            await CreateOkAsync(fixture, inviterUid, NewEmail());

            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.InvitationCreated && a.UserId == inviterId));
        }

        [Fact]
        public async Task The_invite_form_lists_the_internal_domains_and_every_role_with_its_user_type()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await fixture.SendAsync(HttpMethod.Get, $"{InvitationsPath}/form", fixture.CreateToken(inviterUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([IdentitySpecFixture.InternalDomain], body.GetProperty("internalDomains").EnumerateArray().Select(d => d.GetString()));
            var roles = body.GetProperty("roles").EnumerateArray().ToDictionary(
                r => r.GetProperty("key").GetString()!,
                r => (UserType: r.GetProperty("userType").GetString(), RequiresUsersManage: r.GetProperty("requiresUsersManage").GetBoolean()));
            Assert.Equal(("External", false), roles[DefaultRoleNames.User]);
            Assert.Equal(("Internal", false), roles[DefaultRoleNames.ScrapGoUser]);
            Assert.Equal(("Internal", true), roles[DefaultRoleNames.SiteAdmin]);
        }

        [Fact]
        public async Task They_can_invite_staff_as_scrapgo_user_and_get_the_sign_in_link()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewStaffEmail();

            var response = await CreateAsync(fixture, inviterUid, email, [await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)], []);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal($"{IdentitySpecFixture.FrontendBaseUrl}/login", body.GetProperty("acceptLink").GetString());
            var invitation = body.GetProperty("invitation");
            Assert.Equal("Internal", invitation.GetProperty("userType").GetString());
            Assert.Equal([DefaultRoleNames.ScrapGoUser], invitation.GetProperty("roleKeys").EnumerateArray().Select(r => r.GetString()));
            Assert.Empty(invitation.GetProperty("supplierRecordIds").EnumerateArray());
        }

        [Fact]
        public async Task They_can_list_invitations()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var (id, _) = await CreateOkAsync(fixture, inviterUid, NewEmail());

            var response = await fixture.SendAsync(HttpMethod.Get, $"{InvitationsPath}?status=pending&pageSize=100", fixture.CreateToken(inviterUid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(id, body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        }

        // Sad paths, kept separate.

        [Fact]
        public async Task An_internal_role_for_an_external_email_is_rejected()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            var internalRoleId = await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser);

            var response = await CreateAsync(fixture, inviterUid, email, [internalRoleId], [17511]);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("role_type_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email));
        }

        // Staff addresses take internal roles: an external role would make them External.
        [Theory]
        [InlineData("staff-a@scrapgo.example")]
        [InlineData("Staff-B@SCRAPGO.EXAMPLE")]
        public async Task An_external_role_for_an_internal_email_is_rejected(string email)
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await CreateAsync(fixture, inviterUid, email, [await ExternalRoleIdAsync(fixture)], []);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("role_type_mismatch", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email.ToLowerInvariant()));
        }

        // Only users.manage (Site Admin) can create a Site Admin.
        [Fact]
        public async Task Inviting_a_site_admin_is_forbidden()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewStaffEmail();

            var response = await CreateAsync(fixture, inviterUid, email, [await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin)], []);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("site_admin_requires_users_manage", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email));
        }

        // Internal users see every supplier, so links would mean nothing.
        [Fact]
        public async Task Supplier_record_ids_on_an_internal_invitation_are_rejected()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await CreateAsync(fixture, inviterUid, NewStaffEmail(), [await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)], [17511]);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task No_roles_is_rejected()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await CreateAsync(fixture, inviterUid, NewEmail(), [], [17511]);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task No_supplier_record_ids_is_rejected()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);

            var response = await CreateAsync(fixture, inviterUid, NewEmail(), [await ExternalRoleIdAsync(fixture)], []);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        // An existing account gets role changes, not an invitation; any case of the email matches.
        [Fact]
        public async Task An_email_that_already_has_a_user_is_a_conflict()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            await fixture.SeedUserAsync(email);

            var response = await CreateAsync(fixture, inviterUid, email.ToUpperInvariant(), [await ExternalRoleIdAsync(fixture)], [17511]);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("user_exists", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email));
        }

        [Fact]
        public async Task An_existing_staff_member_is_a_conflict_too()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewStaffEmail();
            await fixture.SeedUserAsync(email, UserType.Internal);

            var response = await CreateAsync(fixture, inviterUid, email, [await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)], []);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("user_exists", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }

        [Fact]
        public async Task A_second_pending_invitation_for_the_same_email_is_a_conflict()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            await CreateOkAsync(fixture, inviterUid, email);

            var response = await CreateAsync(fixture, inviterUid, email.ToUpperInvariant(), [await ExternalRoleIdAsync(fixture)], [17511]);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("invitation_pending", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_can_invite_staff_as_site_admin_and_scrapgo_user()
        {
            var inviterUid = await SeedSiteAdminAsync(fixture);
            var email = NewStaffEmail();
            int[] roleIds = [await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin), await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)];

            var response = await CreateAsync(fixture, inviterUid, email, roleIds, []);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var invitation = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invitation");
            Assert.Equal(
                [DefaultRoleNames.ScrapGoUser, DefaultRoleNames.SiteAdmin],
                invitation.GetProperty("roleKeys").EnumerateArray().Select(r => r.GetString()));
        }
    }

    public class Given_an_internal_invitation(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_first_google_sign_in_gets_the_invited_roles_and_accepts_it()
        {
            var email = NewStaffEmail();
            int[] roleIds = [await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin), await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)];
            var id = await CreateStaffOkAsync(fixture, await SeedSiteAdminAsync(fixture), email, roleIds);
            var staffUid = Guid.NewGuid().ToString();

            // No token: Google proves the email.
            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(staffUid, email.ToUpperInvariant()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Internal", me.GetProperty("user").GetProperty("userType").GetString());
            Assert.Equal(
                [DefaultRoleNames.ScrapGoUser, DefaultRoleNames.SiteAdmin],
                me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Order());
            var userId = await UserIdOfAsync(fixture, staffUid);
            var invitation = await fixture.DbContext.Invitations.AsNoTracking().SingleAsync(i => i.Id == id);
            Assert.Equal(InvitationStatus.Accepted, invitation.Status);
            Assert.Equal(userId, invitation.AcceptedByUserId);
            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.InvitationAccepted && a.UserId == userId));
        }

        [Fact]
        public async Task Staff_without_an_invitation_still_get_scrapgo_user()
        {
            var staffUid = Guid.NewGuid().ToString();

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(staffUid, NewStaffEmail()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([DefaultRoleNames.ScrapGoUser], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        [Fact]
        public async Task An_expired_internal_invitation_is_ignored()
        {
            var email = NewStaffEmail();
            var id = await CreateStaffOkAsync(fixture, await SeedSiteAdminAsync(fixture), email, [await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin)]);
            await fixture.DbContext.Invitations.Where(i => i.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(Guid.NewGuid().ToString(), email));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal([DefaultRoleNames.ScrapGoUser], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        // Staff accept with Google only: a password account with the token gets nothing.
        [Fact]
        public async Task A_password_sign_in_with_its_token_is_refused()
        {
            var email = NewStaffEmail();
            var inviterUid = await SeedSiteAdminAsync(fixture);
            var id = await CreateStaffOkAsync(fixture, inviterUid, email, [await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser)]);
            var token = "internal-token";
            await fixture.DbContext.Invitations.Where(i => i.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.TokenHash, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)))));

            await AssertNoAccessAsync(fixture, email, token);
        }
    }

    public class Given_an_external_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_cannot_invite()
        {
            var uid = await SeedExternalUserAsync(fixture);
            var email = NewEmail();

            var response = await CreateAsync(fixture, uid, email, [await ExternalRoleIdAsync(fixture)], [17511]);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("missing_permission", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Invitations.AnyAsync(i => i.Email == email));
        }

        [Fact]
        public async Task They_cannot_read_the_invite_form()
        {
            var uid = await SeedExternalUserAsync(fixture);

            var response = await fixture.SendAsync(HttpMethod.Get, $"{InvitationsPath}/form", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task They_cannot_list_invitations()
        {
            var uid = await SeedExternalUserAsync(fixture);

            var response = await fixture.SendAsync(HttpMethod.Get, InvitationsPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_valid_invitation(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Accepting_creates_an_external_user_with_the_role_and_supplier_links()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            var (id, token) = await CreateOkAsync(fixture, inviterUid, email, [17511, 9583]);
            var inviteeUid = Guid.NewGuid().ToString();

            // The signed-in email matches case-insensitively.
            var response = await AcceptAsync(fixture, inviteeUid, email.ToUpperInvariant(), token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("External", me.GetProperty("user").GetProperty("userType").GetString());
            Assert.Equal([DefaultRoleNames.User], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
            var userId = await UserIdOfAsync(fixture, inviteeUid);

            Assert.Equal(
                [9583, 17511],
                await fixture.DbContext.UserSuppliers.AsNoTracking().Where(s => s.UserId == userId)
                    .OrderBy(s => s.SupplierRecordId).Select(s => s.SupplierRecordId).ToListAsync());
            var invitation = await fixture.DbContext.Invitations.AsNoTracking().SingleAsync(i => i.Id == id);
            Assert.Equal(InvitationStatus.Accepted, invitation.Status);
            Assert.Equal(userId, invitation.AcceptedByUserId);
            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.InvitationAccepted && a.UserId == userId));
        }

        [Fact]
        public async Task Resending_issues_a_new_token_and_the_old_one_stops_working()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            var (id, oldToken) = await CreateOkAsync(fixture, inviterUid, email);

            var resend = await fixture.SendAsync(HttpMethod.Post, $"{InvitationsPath}/{id}/resend", fixture.CreateToken(inviterUid));
            Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
            var newToken = TokenOf((await resend.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("acceptLink").GetString()!);
            Assert.NotEqual(oldToken, newToken);

            await AssertNoAccessAsync(fixture, email, oldToken);
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, newToken)).StatusCode);
        }
    }

    // Sad paths, kept separate: each refusal is 403 no_access and creates no user.

    public class Given_an_invitation_that_cannot_be_accepted(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_expired_invitation_is_refused()
        {
            var email = NewEmail();
            var (id, token) = await CreateOkAsync(fixture, await SeedScrapGoUserAsync(fixture), email);
            await fixture.DbContext.Invitations.Where(i => i.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

            await AssertNoAccessAsync(fixture, email, token);
        }

        [Fact]
        public async Task A_revoked_invitation_is_refused()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            var (id, token) = await CreateOkAsync(fixture, inviterUid, email);

            var revoke = await fixture.SendAsync(HttpMethod.Delete, $"{InvitationsPath}/{id}", fixture.CreateToken(inviterUid));
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

            await AssertNoAccessAsync(fixture, email, token);
        }

        [Fact]
        public async Task A_used_invitation_is_refused()
        {
            var email = NewEmail();
            var (_, token) = await CreateOkAsync(fixture, await SeedScrapGoUserAsync(fixture), email);
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token)).StatusCode);

            await AssertNoAccessAsync(fixture, email, token);
        }

        [Fact]
        public async Task A_different_email_is_refused()
        {
            var (_, token) = await CreateOkAsync(fixture, await SeedScrapGoUserAsync(fixture), NewEmail());

            await AssertNoAccessAsync(fixture, NewEmail(), token);
        }

        [Fact]
        public async Task An_unverified_email_is_refused()
        {
            var email = NewEmail();
            var (_, token) = await CreateOkAsync(fixture, await SeedScrapGoUserAsync(fixture), email);

            await AssertNoAccessAsync(fixture, email, token, emailVerified: false);
        }

        [Fact]
        public async Task An_unknown_token_is_refused() =>
            await AssertNoAccessAsync(fixture, NewEmail(), "not-a-real-token");

        [Fact]
        public async Task An_accepted_invitation_cannot_be_resent_or_revoked()
        {
            var inviterUid = await SeedScrapGoUserAsync(fixture);
            var email = NewEmail();
            var (id, token) = await CreateOkAsync(fixture, inviterUid, email);
            (await AcceptAsync(fixture, Guid.NewGuid().ToString(), email, token)).EnsureSuccessStatusCode();

            var resend = await fixture.SendAsync(HttpMethod.Post, $"{InvitationsPath}/{id}/resend", fixture.CreateToken(inviterUid));
            var revoke = await fixture.SendAsync(HttpMethod.Delete, $"{InvitationsPath}/{id}", fixture.CreateToken(inviterUid));

            Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, revoke.StatusCode);
        }
    }

    private static string NewEmail() => $"buyer-{Guid.NewGuid():N}@acme.example";

    private static string NewStaffEmail() => $"staff-{Guid.NewGuid():N}@{IdentitySpecFixture.InternalDomain}";

    private static async Task AssertNoAccessAsync(IdentitySpecFixture fixture, string email, string token, bool emailVerified = true)
    {
        var uid = Guid.NewGuid().ToString();

        var response = await AcceptAsync(fixture, uid, email, token, emailVerified);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no_access", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        Assert.False(await fixture.DbContext.Users.AsNoTracking().AnyAsync(u => u.IdentityPlatformUid == uid));
    }

    /// <summary>An external invitee signs in with email/password and starts their session with the token.</summary>
    private static Task<HttpResponseMessage> AcceptAsync(
        IdentitySpecFixture fixture, string uid, string email, string token, bool emailVerified = true) =>
        fixture.SendAsync(
            HttpMethod.Post,
            "/api/session",
            fixture.CreateToken(uid, email: email, signInProvider: "password", emailVerified: emailVerified),
            new { invitationToken = token });

    private static Task<HttpResponseMessage> CreateAsync(
        IdentitySpecFixture fixture, string inviterUid, string email, int[] roleIds, int[] supplierRecordIds) =>
        fixture.SendAsync(HttpMethod.Post, InvitationsPath, fixture.CreateToken(inviterUid), new { email, roleIds, supplierRecordIds });

    private static async Task<int> CreateStaffOkAsync(IdentitySpecFixture fixture, string inviterUid, string email, int[] roleIds)
    {
        var response = await CreateAsync(fixture, inviterUid, email, roleIds, []);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invitation").GetProperty("id").GetInt32();
    }

    private static async Task<(int Id, string Token)> CreateOkAsync(
        IdentitySpecFixture fixture, string inviterUid, string email, int[]? supplierRecordIds = null)
    {
        var response = await CreateAsync(fixture, inviterUid, email, [await ExternalRoleIdAsync(fixture)], supplierRecordIds ?? [17511]);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (body.GetProperty("invitation").GetProperty("id").GetInt32(), TokenOf(body.GetProperty("acceptLink").GetString()!));
    }

    private static string TokenOf(string acceptLink) =>
        Uri.UnescapeDataString(acceptLink[(acceptLink.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);

    private static int Base64UrlDecodedLength(string token) => System.Buffers.Text.Base64Url.DecodeFromChars(token).Length;

    private static async Task<string> SeedScrapGoUserAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
        await fixture.AssignRoleAsync(userId, await RoleIdAsync(fixture, DefaultRoleNames.ScrapGoUser));

        return uid;
    }

    private static async Task<string> SeedSiteAdminAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync($"admin-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
        await fixture.AssignRoleAsync(userId, await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin));

        return uid;
    }

    private static async Task<string> SeedExternalUserAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
        fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, await ExternalRoleIdAsync(fixture), UserType.External, null, DateTimeOffset.UtcNow));
        await fixture.DbContext.SaveChangesAsync();
        fixture.EvictAccess();

        return uid;
    }

    private static Task<int> ExternalRoleIdAsync(IdentitySpecFixture fixture) => RoleIdAsync(fixture, DefaultRoleNames.User);

    private static Task<int> RoleIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == key).Select(r => r.Id).SingleAsync();

    private static Task<int> UserIdOfAsync(IdentitySpecFixture fixture, string uid) =>
        fixture.DbContext.Users.AsNoTracking().Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
}
