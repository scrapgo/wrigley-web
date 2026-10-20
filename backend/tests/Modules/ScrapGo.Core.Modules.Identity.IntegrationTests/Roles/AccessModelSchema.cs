// B6: the access-model schema enforces its rules in the database, and the
// seed matches ACCESS-MODEL.md section 6 exactly.
using Npgsql;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class AccessModelSchema
{
    // A role may only be held by a user of the same type: the composite
    // foreign keys (user_id, user_type) and (role_id, user_type) refuse it.
    public class Given_the_site_admin_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_internal_user_can_hold_it()
        {
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var siteAdminId = await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin);

            fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, siteAdminId, UserType.Internal, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            Assert.True(await fixture.DbContext.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.RoleId == siteAdminId));
        }

        [Fact]
        public async Task The_database_rejects_it_for_an_external_user_as_external()
        {
            var (_, userId) = await fixture.SeedUserAsync($"buyer-{Guid.NewGuid():N}@acme.example");
            var siteAdminId = await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin);

            await AssertForeignKeyViolationAsync(fixture,
                $"INSERT INTO identity.user_roles (user_id, role_id, user_type, assigned_at) VALUES ({userId}, {siteAdminId}, 'External', now())");
        }

        [Fact]
        public async Task The_database_rejects_it_for_an_external_user_claimed_as_internal()
        {
            var (_, userId) = await fixture.SeedUserAsync($"buyer-{Guid.NewGuid():N}@acme.example");
            var siteAdminId = await RoleIdAsync(fixture, DefaultRoleNames.SiteAdmin);

            await AssertForeignKeyViolationAsync(fixture,
                $"INSERT INTO identity.user_roles (user_id, role_id, user_type, assigned_at) VALUES ({userId}, {siteAdminId}, 'Internal', now())");
        }
    }

    // Supplier links are for external users only (CHECK user_type = 'External'
    // plus the composite foreign key to the user).
    public class Given_a_supplier_link(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task An_external_user_can_have_one()
        {
            var (_, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");

            fixture.DbContext.UserSuppliers.Add(UserSupplier.Link(userId, supplierRecordId: 17511, linkedByUserId: null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            Assert.True(await fixture.DbContext.UserSuppliers.AsNoTracking().AnyAsync(us => us.UserId == userId && us.SupplierRecordId == 17511));
        }

        [Fact]
        public async Task The_database_rejects_one_for_an_internal_user()
        {
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

            await AssertForeignKeyViolationAsync(fixture,
                $"INSERT INTO identity.user_suppliers (user_id, user_type, supplier_record_id, linked_at) VALUES ({userId}, 'External', 17511, now())");
        }

        [Fact]
        public async Task The_database_rejects_one_marked_internal()
        {
            var (_, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.DbContext.Database.ExecuteSqlAsync(
                $"INSERT INTO identity.user_suppliers (user_id, user_type, supplier_record_id, linked_at) VALUES ({userId}, 'Internal', 17511, now())"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_user_suppliers_external", exception.ConstraintName);
        }
    }

    // Locked features (the admin ones) are always On (CHECK is_toggleable OR state = 'On').
    public class Given_a_feature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_toggleable_feature_can_be_set_off()
        {
            await fixture.DbContext.Database.ExecuteSqlAsync(
                $"UPDATE identity.features SET state = 'Off' WHERE key = {FeatureKeys.Dashboard}");

            Assert.Equal(FeatureState.Off, await StateOfAsync(FeatureKeys.Dashboard));

            await fixture.DbContext.Database.ExecuteSqlAsync(
                $"UPDATE identity.features SET state = 'On' WHERE key = {FeatureKeys.Dashboard}");
        }

        [Fact]
        public async Task The_database_rejects_setting_a_locked_feature_off()
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.DbContext.Database.ExecuteSqlAsync(
                $"UPDATE identity.features SET state = 'Off' WHERE key = {FeatureKeys.Users}"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_features_locked_is_on", exception.ConstraintName);
            Assert.Equal(FeatureState.On, await StateOfAsync(FeatureKeys.Users));
        }

        [Fact]
        public async Task The_domain_refuses_to_toggle_a_locked_feature()
        {
            var users = await fixture.DbContext.Features.AsNoTracking().SingleAsync(f => f.Key == FeatureKeys.Users);

            Assert.Throws<InvalidOperationException>(() => users.ChangeState(FeatureState.Off, changedByUserId: 1, DateTimeOffset.UtcNow));
        }

        private Task<FeatureState> StateOfAsync(string key) =>
            fixture.DbContext.Features.AsNoTracking().Where(f => f.Key == key).Select(f => f.State).SingleAsync();
    }

    // The seed is exactly ACCESS-MODEL.md section 6.
    public class Given_the_seed(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        private static readonly Dictionary<string, string[]> ExpectedMatrix = new()
        {
            [DefaultRoleNames.SiteAdmin] =
            [
                "dashboard.view", "suppliers.view_all", "suppliers.edit_all",
                "users.view", "users.invite", "users.manage",
                "roles.view", "roles.manage", "features.view", "features.manage", "audit.view",
            ],
            [DefaultRoleNames.ScrapGoUser] = ["dashboard.view", "suppliers.view_all", "suppliers.edit_all", "users.invite"],
            [DefaultRoleNames.User] = ["dashboard.view", "suppliers.view_own"],
        };

        [Fact]
        public async Task The_three_system_roles_exist_with_their_user_types()
        {
            var roles = await fixture.DbContext.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Key, r.UserType, r.IsSystem }).ToListAsync();

            Assert.Equal(
                [(DefaultRoleNames.SiteAdmin, UserType.Internal, true), (DefaultRoleNames.ScrapGoUser, UserType.Internal, true), (DefaultRoleNames.User, UserType.External, true)],
                roles.Select(r => (r.Key, r.UserType, r.IsSystem)));
        }

        [Fact]
        public async Task The_role_permission_matrix_matches_the_spec()
        {
            var granted = await fixture.DbContext.RolePermissions.AsNoTracking()
                .Join(fixture.DbContext.Roles.AsNoTracking(), rp => rp.RoleId, r => r.Id, (rp, r) => new { RoleKey = r.Key, PermissionKey = rp.Permission.Key })
                .ToListAsync();

            foreach (var (roleKey, expected) in ExpectedMatrix)
            {
                Assert.Equal(
                    expected.Order(StringComparer.Ordinal),
                    granted.Where(g => g.RoleKey == roleKey).Select(g => g.PermissionKey).Order(StringComparer.Ordinal));
            }

            Assert.All(granted, g => Assert.Contains(g.RoleKey, ExpectedMatrix.Keys));
        }

        [Fact]
        public async Task Suppliers_edit_own_is_defined_but_granted_to_nobody()
        {
            Assert.True(await fixture.DbContext.Permissions.AsNoTracking().AnyAsync(p => p.Key == Permissions.SuppliersEditOwn));
            Assert.False(await fixture.DbContext.RolePermissions.AsNoTracking().AnyAsync(rp => rp.Permission.Key == Permissions.SuppliersEditOwn));
        }

        [Fact]
        public async Task The_features_have_their_launch_states_and_locks()
        {
            var features = await fixture.DbContext.Features.AsNoTracking().Select(f => new { f.Key, f.State, f.IsToggleable }).ToListAsync();

            Assert.Equal(
                new[]
                {
                    ("audit", FeatureState.On, false),
                    ("dashboard", FeatureState.On, true),
                    ("features", FeatureState.On, false),
                    ("roles", FeatureState.On, false),
                    ("supplier_edit", FeatureState.Targeted, true),
                    ("suppliers", FeatureState.On, true),
                    ("users", FeatureState.On, false),
                },
                features.Select(f => (f.Key, f.State, f.IsToggleable)).OrderBy(f => f.Key, StringComparer.Ordinal));
        }

        [Fact]
        public async Task Each_permission_has_its_feature_and_audience()
        {
            var permissions = await fixture.DbContext.Permissions.AsNoTracking()
                .Where(p => Permissions.All.Contains(p.Key))
                .Join(fixture.DbContext.Features.AsNoTracking(), p => p.FeatureId, f => f.Id, (p, f) => new { p.Key, Feature = f.Key, p.Audience })
                .ToListAsync();

            Assert.Equal(
                new[]
                {
                    ("audit.view", "audit", PermissionAudience.Internal),
                    ("dashboard.view", "dashboard", PermissionAudience.Any),
                    ("features.manage", "features", PermissionAudience.Internal),
                    ("features.view", "features", PermissionAudience.Internal),
                    ("roles.manage", "roles", PermissionAudience.Internal),
                    ("roles.view", "roles", PermissionAudience.Internal),
                    ("suppliers.edit_all", "supplier_edit", PermissionAudience.Internal),
                    ("suppliers.edit_own", "supplier_edit", PermissionAudience.External),
                    ("suppliers.view_all", "suppliers", PermissionAudience.Internal),
                    ("suppliers.view_own", "suppliers", PermissionAudience.External),
                    ("users.invite", "users", PermissionAudience.Internal),
                    ("users.manage", "users", PermissionAudience.Internal),
                    ("users.view", "users", PermissionAudience.Internal),
                },
                permissions.Select(p => (p.Key, p.Feature, p.Audience)).OrderBy(p => p.Key, StringComparer.Ordinal));
        }
    }

    private static Task<int> RoleIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == key).Select(r => r.Id).SingleAsync();

    private static async Task AssertForeignKeyViolationAsync(IdentitySpecFixture fixture, FormattableString sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.DbContext.Database.ExecuteSqlAsync(sql));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }
}
