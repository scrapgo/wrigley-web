// ADMIN-API-STATUS Task 1: Admin.Access is reachable only through an explicit,
// one-time PlatformAdministrator bootstrap. Deny-by-default holds for everyone else.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Authorization;
using ScrapGo.Core.Shared.Kernel.Audit;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Authorization;

public class PlatformAdministratorBootstrap
{
    private const string PlatformScopedPath = "/api/_test/platform-scoped";

    public class Given_a_provisioned_user_who_is_bootstrapped(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_admin_access_check_is_allowed()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            Assert.Equal(BootstrapPlatformAdministratorOutcome.Granted, await BootstrapAsync(fixture, uid));

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class Given_the_same_user_is_bootstrapped_twice(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_second_run_is_a_no_op_with_one_grant_and_one_audit_row()
        {
            var (uid, userId) = await fixture.SeedUserAsync();

            Assert.Equal(BootstrapPlatformAdministratorOutcome.Granted, await BootstrapAsync(fixture, uid));
            Assert.Equal(BootstrapPlatformAdministratorOutcome.AlreadyGranted, await BootstrapAsync(fixture, uid));

            Assert.Equal(1, await fixture.DbContext.UserRoles.AsNoTracking().CountAsync(ur =>
                ur.UserId == userId
                && ur.OrganizationId == null
                && ur.Role.Name == DefaultRoleNames.PlatformAdministrator));

            var audit = await fixture.DbContext.AuditLogs.AsNoTracking()
                .Where(a => a.EventType == IdentityAuditEventTypes.PlatformAdministratorBootstrapped)
                .ToListAsync();
            var entry = Assert.Single(audit);
            Assert.Equal(AuditActorType.System, entry.ActorType);
            Assert.Null(entry.UserId);
            Assert.Contains(uid, entry.Metadata);
        }
    }

    // Sad paths, kept separate.

    public class Given_an_authenticated_user_with_no_roles(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_admin_access_check_is_denied()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Signing in never grants anything: provisioning through /api/users/me
    // leaves the caller with no platform role.
    public class Given_a_user_who_only_signed_in(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_hold_no_role_and_are_denied()
        {
            var uid = Guid.NewGuid().ToString();
            var token = fixture.CreateToken(uid, email: "fresh@example.com");
            (await fixture.SendUsersMeAsync(token)).EnsureSuccessStatusCode();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformScopedPath, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var userId = await fixture.DbContext.Users.AsNoTracking()
                .Where(u => u.IdentityPlatformUid == uid).Select(u => u.Id).SingleAsync();
            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId));
        }
    }

    // OrganizationAdministrator never carries Admin.Access, and its grants are
    // organization-scoped, so creating an organization is no path to platform admin.
    public class Given_an_organization_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_platform_scoped_admin_access_check_is_denied()
        {
            var (uid, _, _) = await fixture.SeedOrganizationAdministratorAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, PlatformScopedPath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_platform_administrator_already_exists(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrapping_a_different_user_is_refused()
        {
            var (firstUid, _) = await fixture.SeedUserAsync();
            var (secondUid, secondUserId) = await fixture.SeedUserAsync("second@example.com");
            Assert.Equal(BootstrapPlatformAdministratorOutcome.Granted, await BootstrapAsync(fixture, firstUid));

            Assert.Equal(BootstrapPlatformAdministratorOutcome.AnotherAdministratorExists, await BootstrapAsync(fixture, secondUid));

            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == secondUserId));
            var response = await fixture.SendAsync(HttpMethod.Get, PlatformScopedPath, fixture.CreateToken(secondUid));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_a_uid_that_was_never_provisioned(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Bootstrap_reports_user_not_provisioned_and_writes_nothing()
        {
            Assert.Equal(
                BootstrapPlatformAdministratorOutcome.UserNotProvisioned,
                await BootstrapAsync(fixture, Guid.NewGuid().ToString()));

            Assert.False(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.OrganizationId == null));
            Assert.False(await fixture.DbContext.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.EventType == IdentityAuditEventTypes.PlatformAdministratorBootstrapped));
        }
    }

    /// <summary>Runs the handler the way the CLI command does: in its own DI scope.</summary>
    private static async Task<BootstrapPlatformAdministratorOutcome> BootstrapAsync(IdentitySpecFixture fixture, string uid)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<BootstrapPlatformAdministratorHandler>()
            .HandleAsync(new BootstrapPlatformAdministratorCommand(uid), CancellationToken.None);
    }
}
