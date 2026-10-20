using System.Net.Http.Json;
using System.Text.Json;

// STORY-009, B10: the first POST /api/session for an internal UID creates its user exactly once.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class FirstRequestAutoProvisioningIsExactlyOnce
{
    public class Given_a_workspace_sign_in_for_an_unknown_uid(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Starting_a_session_creates_exactly_one_user_row()
        {
            var uid = Guid.NewGuid().ToString();

            (await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid))).EnsureSuccessStatusCode();

            Assert.Equal(1, await fixture.DbContext.Users.CountAsync(u => u.IdentityPlatformUid == uid));
        }

        [Fact]
        public async Task A_user_provisioned_audit_row_is_written_in_the_audit_schema()
        {
            var uid = Guid.NewGuid().ToString();

            (await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid))).EnsureSuccessStatusCode();

            var userId = await fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.UserProvisioned && a.UserId == userId));
        }

        [Fact]
        public async Task The_response_is_the_new_users_access()
        {
            var uid = Guid.NewGuid().ToString();

            var response = await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(uid));

            var me = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Internal", me.GetProperty("user").GetProperty("userType").GetString());
            Assert.Equal([DefaultRoleNames.ScrapGoUser], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
            Assert.Contains(Permissions.DashboardView, me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        }

        // GET /api/me never provisions: that was /api/users/me's old behaviour.
        [Fact]
        public async Task Calling_me_first_returns_four_hundred_three_no_access_and_creates_no_row()
        {
            var uid = Guid.NewGuid().ToString();

            var response = await fixture.SendMeAsync(fixture.CreateWorkspaceToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("no_access", await IdentitySpecFixture.ReadProblemReasonAsync(response));
            Assert.False(await fixture.DbContext.Users.AnyAsync(u => u.IdentityPlatformUid == uid));
        }
    }

    public class Given_a_user_row_already_exists_for_the_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();

        public async Task InitializeAsync()
        {
            fixture.DbContext.Users.Add(User.Provision(_uid, "existing@scrapgo.example", UserType.Internal, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Starting_a_session_does_not_create_another_row()
        {
            (await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(_uid))).EnsureSuccessStatusCode();

            Assert.Equal(1, await fixture.DbContext.Users.CountAsync(u => u.IdentityPlatformUid == _uid));
        }

        [Fact]
        public async Task Starting_a_session_does_not_write_a_user_provisioned_audit_row()
        {
            (await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(_uid))).EnsureSuccessStatusCode();

            var userId = await fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == _uid).Select(u => u.Id).SingleAsync();
            Assert.Equal(0, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.UserProvisioned && a.UserId == userId));
        }

        [Fact]
        public async Task Starting_a_session_records_the_sign_in()
        {
            (await fixture.StartSessionAsync(fixture.CreateWorkspaceToken(_uid))).EnsureSuccessStatusCode();

            var user = await fixture.DbContext.Users.AsNoTracking().SingleAsync(u => u.IdentityPlatformUid == _uid);
            Assert.NotNull(user.LastSignInAt);
            Assert.True(await fixture.DbContext.AuditLogs.AnyAsync(
                a => a.EventType == IdentityAuditEventTypes.SessionStarted && a.UserId == user.Id));
        }
    }

    // The core claim: no read-then-write race. Ten truly concurrent first
    // sessions for one never-seen UID, through the real HTTP pipeline and
    // real Postgres, must leave exactly one row and one role. That comes from
    // the DB-level ON CONFLICT DO NOTHING, not from application locking.
    public class Given_ten_concurrent_first_sessions_for_the_same_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Exactly_one_user_row_and_one_role_exist_after_all_settle()
        {
            var uid = Guid.NewGuid().ToString();
            var token = fixture.CreateWorkspaceToken(uid);

            var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.StartSessionAsync(token)));

            Assert.All(responses, response => response.EnsureSuccessStatusCode());
            var userId = await fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            Assert.Equal(1, await fixture.DbContext.UserRoles.CountAsync(ur => ur.UserId == userId));
        }
    }
}
