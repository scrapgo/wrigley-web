// STORY-009: First-request auto-provisioning is exactly-once.

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class FirstRequestAutoProvisioningIsExactlyOnce
{
    public class Given_a_valid_jwt_for_an_unknown_uid(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Calling_users_me_creates_exactly_one_user_row()
        {
            var uid = Guid.NewGuid().ToString();

            (await fixture.SendUsersMeAsync(fixture.CreateToken(uid))).EnsureSuccessStatusCode();

            Assert.Equal(1, await fixture.DbContext.Users.CountAsync(u => u.IdentityPlatformUid == uid));
        }

        [Fact]
        public async Task A_user_provisioned_audit_row_is_written_in_the_audit_schema()
        {
            var uid = Guid.NewGuid().ToString();

            (await fixture.SendUsersMeAsync(fixture.CreateToken(uid))).EnsureSuccessStatusCode();

            var userId = await fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            Assert.Equal(1, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.UserProvisioned && a.UserId == userId));
        }
    }

    public class Given_a_user_row_already_exists_for_the_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>, IAsyncLifetime
    {
        private readonly string _uid = Guid.NewGuid().ToString();

        public async Task InitializeAsync()
        {
            fixture.DbContext.Users.Add(User.Provision(_uid, "existing@example.com", UserClassification.External, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Calling_users_me_does_not_create_another_row()
        {
            (await fixture.SendUsersMeAsync(fixture.CreateToken(_uid))).EnsureSuccessStatusCode();

            Assert.Equal(1, await fixture.DbContext.Users.CountAsync(u => u.IdentityPlatformUid == _uid));
        }

        [Fact]
        public async Task Calling_users_me_does_not_write_a_user_provisioned_audit_row()
        {
            (await fixture.SendUsersMeAsync(fixture.CreateToken(_uid))).EnsureSuccessStatusCode();

            var userId = await fixture.DbContext.Users.Where(u => u.IdentityPlatformUid == _uid).Select(u => u.Id).SingleAsync();
            Assert.Equal(0, await fixture.DbContext.AuditLogs.CountAsync(
                a => a.EventType == IdentityAuditEventTypes.UserProvisioned && a.UserId == userId));
        }
    }

    // The core claim: no read-then-write race. Ten truly concurrent first
    // requests for one never-seen UID, through the real HTTP pipeline and
    // real Postgres, must leave exactly one row. That comes from the
    // DB-level ON CONFLICT DO NOTHING, not from application locking.
    public class Given_ten_concurrent_first_requests_for_the_same_uid(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Exactly_one_user_row_exists_after_all_settle()
        {
            var uid = Guid.NewGuid().ToString();
            var token = fixture.CreateToken(uid);

            var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.SendUsersMeAsync(token)));

            Assert.All(responses, response => response.EnsureSuccessStatusCode());
            Assert.Equal(1, await fixture.DbContext.Users.CountAsync(u => u.IdentityPlatformUid == uid));
        }
    }
}
