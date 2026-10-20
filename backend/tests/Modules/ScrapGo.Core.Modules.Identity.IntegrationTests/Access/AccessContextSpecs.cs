// B7: the access context applies the access rule of ACCESS-MODEL.md section 4,
// and the cache serves it for up to 30 seconds unless evicted.
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Modules.Identity.Application.Access;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Access;

public class AccessContextSpecs
{
    public class Given_an_active_internal_user_with_a_role(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_get_the_roles_permissions_whose_features_are_on()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            var context = await LoadAsync(fixture, uid);

            Assert.NotNull(context);
            Assert.Equal(userId, context.UserId);
            Assert.Equal(UserType.Internal, context.UserType);
            Assert.Equal([DefaultRoleNames.ScrapGoUser], context.Roles);
            Assert.True(context.Can(Permissions.SuppliersViewAll));
            Assert.True(context.Can(Permissions.DashboardView));
            Assert.Contains(FeatureKeys.Suppliers, context.Features);
            Assert.Empty(context.SupplierRecordIds);
        }

        [Fact]
        public async Task They_never_get_a_permission_no_role_grants()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            var context = await LoadAsync(fixture, uid);

            Assert.False(context!.Can(Permissions.UsersManage));
            Assert.DoesNotContain(Permissions.UsersManage, context.Permissions);
        }

        [Fact]
        public async Task An_unknown_uid_has_no_context() =>
            Assert.Null(await LoadAsync(fixture, Guid.NewGuid().ToString()));
    }

    public class Given_an_external_user_with_supplier_links(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_see_their_own_suppliers_permission_and_record_ids()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.User, UserType.External);
            fixture.DbContext.UserSuppliers.AddRange(
                UserSupplier.Link(userId, 17511, null, DateTimeOffset.UtcNow),
                UserSupplier.Link(userId, 9583, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            var context = await LoadAsync(fixture, uid);

            Assert.True(context!.Can(Permissions.SuppliersViewOwn));
            Assert.False(context.Can(Permissions.SuppliersViewAll));
            Assert.Equal([9583, 17511], context.SupplierRecordIds.Order());
        }
    }

    public class Given_a_disabled_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task They_get_no_permissions_even_though_their_roles_grant_some()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.SiteAdmin, UserType.Internal);
            var user = await fixture.DbContext.Users.SingleAsync(u => u.Id == userId);
            user.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var context = await LoadAsync(fixture, uid);

            Assert.Equal(UserStatus.Disabled, context!.Status);
            Assert.Empty(context.Permissions);
            Assert.False(context.Can(Permissions.DashboardView));
        }
    }

    // A feature that is Off hides its permissions from everyone.
    public class Given_a_feature_that_is_off(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Its_permissions_are_not_effective()
        {
            await SetFeatureStateAsync(fixture, FeatureKeys.Dashboard, FeatureState.Off);
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            var context = await LoadAsync(fixture, uid);

            Assert.False(context!.Can(Permissions.DashboardView));
            Assert.DoesNotContain(FeatureKeys.Dashboard, context.Features);
            Assert.True(context.Can(Permissions.SuppliersViewAll));
        }
    }

    // supplier_edit is seeded Targeted, and scrapgo_user holds suppliers.edit_all.
    public class Given_a_targeted_feature(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task A_user_target_turns_it_on_for_that_user()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            await AddTargetAsync(fixture, FeatureTarget.ForUser(await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), userId));

            var context = await LoadAsync(fixture, uid);

            Assert.True(context!.Can(Permissions.SuppliersEditAll));
            Assert.Contains(FeatureKeys.SupplierEdit, context.Features);
        }

        [Fact]
        public async Task A_role_target_turns_it_on_for_holders_of_that_role()
        {
            var roleId = await CreateRoleAsync(fixture, Permissions.SuppliersEditAll);
            var (uid, userId) = await SeedInternalAsync(fixture);
            fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, roleId, UserType.Internal, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();
            await AddTargetAsync(fixture, FeatureTarget.ForRole(await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), roleId));

            var context = await LoadAsync(fixture, uid);

            Assert.True(context!.Can(Permissions.SuppliersEditAll));
        }

        [Fact]
        public async Task With_no_matching_target_it_stays_off()
        {
            var (_, otherId) = await SeedInternalAsync(fixture);
            await AddTargetAsync(fixture, FeatureTarget.ForUser(await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), otherId));
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            var context = await LoadAsync(fixture, uid);

            Assert.False(context!.Can(Permissions.SuppliersEditAll));
            Assert.DoesNotContain(FeatureKeys.SupplierEdit, context.Features);
        }

        // A toggle never grants access: a target without the permission gets nothing.
        [Fact]
        public async Task A_target_whose_roles_lack_the_permission_gets_nothing()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            await AddTargetAsync(fixture, FeatureTarget.ForUser(await FeatureIdAsync(fixture, FeatureKeys.SupplierEdit), userId));

            var context = await LoadAsync(fixture, uid);

            Assert.Contains(FeatureKeys.SupplierEdit, context!.Features);
            Assert.False(context.Can(Permissions.SuppliersEditAll));
        }
    }

    public class Given_a_cached_context(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task It_is_served_from_the_cache_until_evicted()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            Assert.Empty((await CachedAsync(fixture, uid))!.Roles);

            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            // Still the cached copy: the change isn't visible yet.
            Assert.Empty((await CachedAsync(fixture, uid))!.Roles);

            Cache(fixture).EvictUser(userId);

            Assert.Equal([DefaultRoleNames.ScrapGoUser], (await CachedAsync(fixture, uid))!.Roles);
        }

        [Fact]
        public async Task Evicting_one_user_leaves_the_others_cached()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            var (otherUid, otherId) = await SeedInternalAsync(fixture);
            await CachedAsync(fixture, uid);
            await CachedAsync(fixture, otherUid);

            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            await AssignSeededRoleAsync(fixture, otherId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            Cache(fixture).EvictUser(userId);

            Assert.NotEmpty((await CachedAsync(fixture, uid))!.Roles);
            Assert.Empty((await CachedAsync(fixture, otherUid))!.Roles);
        }

        [Fact]
        public async Task Evict_all_drops_every_cached_context()
        {
            var (uid, userId) = await SeedInternalAsync(fixture);
            var (otherUid, otherId) = await SeedInternalAsync(fixture);
            await CachedAsync(fixture, uid);
            await CachedAsync(fixture, otherUid);

            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            await AssignSeededRoleAsync(fixture, otherId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            Cache(fixture).EvictAll();

            Assert.NotEmpty((await CachedAsync(fixture, uid))!.Roles);
            Assert.NotEmpty((await CachedAsync(fixture, otherUid))!.Roles);
        }
    }

    private static Task<(string Uid, int UserId)> SeedInternalAsync(IdentitySpecFixture fixture) =>
        fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);

    private static async Task AssignSeededRoleAsync(IdentitySpecFixture fixture, int userId, string roleKey, UserType userType)
    {
        var roleId = await fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == roleKey).Select(r => r.Id).SingleAsync();
        fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, roleId, userType, null, DateTimeOffset.UtcNow));
        await fixture.DbContext.SaveChangesAsync();
    }

    private static async Task<int> CreateRoleAsync(IdentitySpecFixture fixture, string permission)
    {
        var role = Role.Create($"role_{Guid.NewGuid():N}", "Spec role", null, UserType.Internal, isSystem: false);
        fixture.DbContext.Roles.Add(role);
        await fixture.DbContext.SaveChangesAsync();

        var permissionId = await fixture.DbContext.Permissions.AsNoTracking().Where(p => p.Key == permission).Select(p => p.Id).SingleAsync();
        fixture.DbContext.RolePermissions.Add(RolePermission.Create(role.Id, permissionId));
        await fixture.DbContext.SaveChangesAsync();

        return role.Id;
    }

    private static Task<int> FeatureIdAsync(IdentitySpecFixture fixture, string key) =>
        fixture.DbContext.Features.AsNoTracking().Where(f => f.Key == key).Select(f => f.Id).SingleAsync();

    private static async Task AddTargetAsync(IdentitySpecFixture fixture, FeatureTarget target)
    {
        fixture.DbContext.FeatureTargets.Add(target);
        await fixture.DbContext.SaveChangesAsync();
    }

    private static Task SetFeatureStateAsync(IdentitySpecFixture fixture, string key, FeatureState state) =>
        fixture.DbContext.Database.ExecuteSqlAsync($"UPDATE identity.features SET state = {state.ToString()} WHERE key = {key}");

    /// <summary>Straight from the database, through the provider.</summary>
    private static async Task<AccessContext?> LoadAsync(IdentitySpecFixture fixture, string uid)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccessContextProvider>().LoadAsync(uid, CancellationToken.None);
    }

    /// <summary>Through the singleton cache, loading from a fresh scope on a miss.</summary>
    private static async Task<AccessContext?> CachedAsync(IdentitySpecFixture fixture, string uid)
    {
        using var scope = fixture.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAccessContextProvider>();
        return await Cache(fixture).GetOrLoadAsync(uid, ct => provider.LoadAsync(uid, ct), CancellationToken.None);
    }

    private static IAccessCache Cache(IdentitySpecFixture fixture) => fixture.Services.GetRequiredService<IAccessCache>();
}
