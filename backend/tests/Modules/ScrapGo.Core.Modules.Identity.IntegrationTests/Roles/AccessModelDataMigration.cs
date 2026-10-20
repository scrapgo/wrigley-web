// B6-alt: AccessModelV1 is a forward migration over real data. Existing users
// are kept (classification becomes user_type) and get the new system roles.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;
using ScrapGo.Core.Shared.Infrastructure.Audit;
using ScrapGo.Core.Shared.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class AccessModelDataMigration
{
    /// <summary>The last migration of the old organization / application model.</summary>
    private const string LastOldMigration = "20261007022849_AddDownstreamApplication";

    /// <summary>A database with old-model data, then migrated to the latest model. Its own container: it starts from an old schema.</summary>
    public sealed class OldModelDatabase : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

        public IdentityDbContext DbContext { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            // The audit schema first, as in production: old Identity migrations write audit rows.
            await using (var audit = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>()
                .UseModulePostgres(_container.GetConnectionString(), AuditLogConfiguration.Schema)
                .Options))
            {
                await audit.Database.MigrateAsync();
            }

            DbContext = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseModulePostgres(_container.GetConnectionString(), IdentityDbContext.Schema)
                .Options);

            var migrator = DbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(LastOldMigration);

            // Old-model data: a platform administrator, an internal member, and an
            // external organization administrator.
            await DbContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO identity.users (identity_platform_uid, email, status, classification, created_at, updated_at) VALUES
                    ('uid-admin', 'admin@scrapgo.com', 'Active', 'Internal', now(), now()),
                    ('uid-staff', 'staff@scrapgo.com', 'Active', 'Internal', now(), now()),
                    ('uid-customer', 'buyer@acme.example', 'Active', 'External', now(), now());

                INSERT INTO identity.organizations (name, slug, status, created_at, updated_at)
                VALUES ('Acme', 'acme', 'Active', now(), now());

                INSERT INTO identity.user_roles (user_id, role_id, organization_id, created_at, updated_at)
                SELECT u.id, r.id, NULL, now(), now()
                FROM identity.users u, identity.roles r
                WHERE u.identity_platform_uid = 'uid-admin' AND r.name = 'PlatformAdministrator';

                INSERT INTO identity.user_roles (user_id, role_id, organization_id, created_at, updated_at)
                SELECT u.id, r.id, o.id, now(), now()
                FROM identity.users u, identity.roles r, identity.organizations o
                WHERE u.identity_platform_uid = 'uid-customer' AND r.name = 'OrganizationAdministrator' AND o.slug = 'acme';
                """);

            await migrator.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _container.DisposeAsync();
        }
    }

    public class Given_a_database_with_old_model_data(OldModelDatabase database) : IClassFixture<OldModelDatabase>
    {
        [Fact]
        public async Task Every_user_is_kept_with_their_classification_as_user_type()
        {
            var users = await database.DbContext.Users.AsNoTracking()
                .OrderBy(u => u.IdentityPlatformUid)
                .Select(u => new { u.IdentityPlatformUid, u.Email, u.UserType })
                .ToListAsync();

            Assert.Equal(
                [("uid-admin", "admin@scrapgo.com", UserType.Internal), ("uid-customer", "buyer@acme.example", UserType.External), ("uid-staff", "staff@scrapgo.com", UserType.Internal)],
                users.Select(u => (u.IdentityPlatformUid, u.Email, u.UserType)));
        }

        [Fact]
        public async Task The_former_platform_administrator_is_a_site_admin_and_a_scrapgo_user() =>
            Assert.Equal([DefaultRoleNames.ScrapGoUser, DefaultRoleNames.SiteAdmin], await RoleKeysOfAsync("uid-admin"));

        [Fact]
        public async Task Another_internal_user_is_a_scrapgo_user_only() =>
            Assert.Equal([DefaultRoleNames.ScrapGoUser], await RoleKeysOfAsync("uid-staff"));

        // Their old organization role grants nothing in the new model.
        [Fact]
        public async Task An_external_user_is_a_user_only() =>
            Assert.Equal([DefaultRoleNames.User], await RoleKeysOfAsync("uid-customer"));

        [Fact]
        public async Task The_old_tables_are_gone()
        {
            var tables = await database.DbContext.Database.SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'identity'").ToListAsync();

            Assert.DoesNotContain("organizations", tables);
            Assert.DoesNotContain("organization_memberships", tables);
            Assert.DoesNotContain("applications", tables);
            Assert.DoesNotContain("linked_credentials", tables);
            Assert.Contains("features", tables);
            Assert.Contains("user_suppliers", tables);
        }

        [Fact]
        public async Task New_rows_get_ids_after_the_seed()
        {
            var role = Role.Create($"probe_{Guid.NewGuid():N}", "Probe", null, UserType.Internal, isSystem: false);
            database.DbContext.Roles.Add(role);
            await database.DbContext.SaveChangesAsync();

            Assert.True(role.Id > 3);
        }

        private async Task<List<string>> RoleKeysOfAsync(string uid) =>
            (await database.DbContext.UserRoles.AsNoTracking()
                .Join(database.DbContext.Users.AsNoTracking(), ur => ur.UserId, u => u.Id, (ur, u) => new { u.IdentityPlatformUid, ur.Role.Key })
                .Where(x => x.IdentityPlatformUid == uid)
                .Select(x => x.Key)
                .ToListAsync())
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
