// B12: site_admin is reachable outside the role API only through the audited
// bootstrap-site-admin command, and only while no active Site Admin exists.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Shared.Kernel.Audit;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

// One database per class: the "no active Site Admin" rule is global state.
public class SiteAdminBootstrap
{
    private const string UsersManagePath = "/api/_test/users-manage";

    public class Given_an_internal_user_and_no_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrapping_grants_site_admin_and_administration_is_allowed()
        {
            var (uid, userId) = await fixture.SeedUserAsync("first-admin@scrapgo.example", UserType.Internal);

            Assert.Equal(BootstrapSiteAdminOutcome.Granted, await BootstrapAsync(fixture, uid));

            Assert.Equal([DefaultRoleNames.SiteAdmin], await RoleKeysOfAsync(fixture, userId));
            var response = await fixture.SendAsync(HttpMethod.Get, UsersManagePath, fixture.CreateToken(uid));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_the_same_user_is_bootstrapped_twice(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_second_run_is_a_no_op_with_one_grant_and_one_system_audit_row()
        {
            var (uid, userId) = await fixture.SeedUserAsync("first-admin@scrapgo.example", UserType.Internal);

            Assert.Equal(BootstrapSiteAdminOutcome.Granted, await BootstrapAsync(fixture, uid));
            Assert.Equal(BootstrapSiteAdminOutcome.AlreadyGranted, await BootstrapAsync(fixture, uid));

            Assert.Equal([DefaultRoleNames.SiteAdmin], await RoleKeysOfAsync(fixture, userId));
            var entry = Assert.Single(await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.SiteAdminBootstrapped)
                .ToListAsync());
            Assert.Equal(AuditActorType.System, entry.ActorType);
            Assert.Null(entry.UserId);
            Assert.Contains(uid, entry.Metadata);
        }
    }

    // Disabled Site Admins can't administer, so they don't block recovery.
    public class Given_the_only_site_admin_is_disabled(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrapping_another_internal_user_is_allowed()
        {
            var (_, disabledAdminId) = await fixture.SeedSiteAdminAsync();
            var disabledAdmin = await fixture.DbContext.Users.SingleAsync(u => u.Id == disabledAdminId);
            disabledAdmin.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();
            var (uid, _) = await fixture.SeedUserAsync("recovery@scrapgo.example", UserType.Internal);

            Assert.Equal(BootstrapSiteAdminOutcome.Granted, await BootstrapAsync(fixture, uid));
        }
    }

    // Sad paths, kept separate.

    public class Given_an_active_site_admin_exists(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrapping_a_different_user_is_refused_and_writes_nothing()
        {
            await fixture.SeedSiteAdminAsync();
            var (secondUid, secondUserId) = await fixture.SeedUserAsync("second@scrapgo.example", UserType.Internal);

            Assert.Equal(BootstrapSiteAdminOutcome.ActiveSiteAdminExists, await BootstrapAsync(fixture, secondUid));

            Assert.Empty(await RoleKeysOfAsync(fixture, secondUserId));
            Assert.False(await fixture.DbContext.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.EventType == IdentityAuditEventTypes.SiteAdminBootstrapped));
            var response = await fixture.SendAsync(HttpMethod.Get, UsersManagePath, fixture.CreateToken(secondUid));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_external_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrapping_them_is_refused_and_writes_nothing()
        {
            var (uid, userId) = await fixture.SeedUserAsync("customer@acme.example");

            Assert.Equal(BootstrapSiteAdminOutcome.ExternalUserNotAllowed, await BootstrapAsync(fixture, uid));

            Assert.Empty(await RoleKeysOfAsync(fixture, userId));
        }
    }

    public class Given_an_authenticated_user_with_no_roles(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_admin_access_check_is_denied()
        {
            var (uid, _) = await fixture.SeedUserAsync(userType: UserType.Internal);

            var response = await fixture.SendAsync(HttpMethod.Get, UsersManagePath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Signing in never makes anyone an administrator: a new internal user
    // gets scrapgo_user only, never site_admin.
    public class Given_an_internal_user_who_only_signed_in(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_hold_only_scrapgo_user_and_are_denied_administration()
        {
            var uid = Guid.NewGuid().ToString();
            var token = fixture.CreateWorkspaceToken(uid);
            (await fixture.StartSessionAsync(token)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, UsersManagePath, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var userId = await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            Assert.Equal([DefaultRoleNames.ScrapGoUser], await RoleKeysOfAsync(fixture, userId));
        }
    }

    public class Given_a_uid_that_was_never_provisioned(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrap_reports_user_not_provisioned_and_writes_nothing()
        {
            Assert.Equal(BootstrapSiteAdminOutcome.UserNotProvisioned, await BootstrapAsync(fixture, Guid.NewGuid().ToString()));

            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync());
            Assert.False(await fixture.DbContext.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.EventType == IdentityAuditEventTypes.SiteAdminBootstrapped));
        }
    }

    /// <summary>Runs the handler the way the CLI command does: in its own DI scope.</summary>
    private static async Task<BootstrapSiteAdminOutcome> BootstrapAsync(IdentitySpecFixture fixture, string uid)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<BootstrapSiteAdminHandler>()
            .HandleAsync(new BootstrapSiteAdminCommand(uid), CancellationToken.None);
    }

    private static Task<List<string>> RoleKeysOfAsync(IdentitySpecFixture fixture, int userId) =>
        fixture.DbContext.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.Role.Key).ToListAsync();
}
