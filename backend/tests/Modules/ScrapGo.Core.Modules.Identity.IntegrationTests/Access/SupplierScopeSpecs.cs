// B9: IUserContext.GetSupplierScopeAsync, the scope other modules filter suppliers by.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Access;

public class SupplierScopeSpecs
{
    private const string ScopePath = "/api/_test/supplier-scope";

    public class Given_an_internal_user_with_suppliers_view_all(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_scope_is_all_suppliers()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);

            var scope = await GetScopeAsync(fixture, uid);

            Assert.True(scope.GetProperty("all").GetBoolean());
        }
    }

    public class Given_an_external_user_with_suppliers_view_own(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_scope_is_their_linked_suppliers()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.User, UserType.External);
            fixture.DbContext.UserSuppliers.AddRange(
                UserSupplier.Link(userId, 17511, null, DateTimeOffset.UtcNow),
                UserSupplier.Link(userId, 9583, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();
            fixture.EvictAccess();

            var scope = await GetScopeAsync(fixture, uid);

            Assert.False(scope.GetProperty("all").GetBoolean());
            Assert.Equal([9583, 17511], scope.GetProperty("recordIds").EnumerateArray().Select(id => id.GetInt32()));
        }
    }

    // Sad paths, kept separate.

    public class Given_a_user_with_no_supplier_permission(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_scope_is_empty_even_with_supplier_links()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
            fixture.DbContext.UserSuppliers.Add(UserSupplier.Link(userId, 17511, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();
            fixture.EvictAccess();

            var scope = await GetScopeAsync(fixture, uid);

            Assert.False(scope.GetProperty("all").GetBoolean());
            Assert.Equal(0, scope.GetProperty("recordIds").GetArrayLength());
        }

        [Fact]
        public async Task An_unknown_uid_has_an_empty_scope()
        {
            var scope = await GetScopeAsync(fixture, Guid.NewGuid().ToString());

            Assert.False(scope.GetProperty("all").GetBoolean());
            Assert.Equal(0, scope.GetProperty("recordIds").GetArrayLength());
        }
    }

    public class Given_a_disabled_internal_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_gate_refuses_them_before_any_scope_is_read()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            await AssignSeededRoleAsync(fixture, userId, DefaultRoleNames.ScrapGoUser, UserType.Internal);
            var user = await fixture.DbContext.Users.SingleAsync(u => u.Id == userId);
            user.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();
            fixture.EvictAccess();

            var response = await fixture.SendAsync(HttpMethod.Get, ScopePath, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static async Task AssignSeededRoleAsync(IdentitySpecFixture fixture, int userId, string roleKey, UserType userType)
    {
        var roleId = await fixture.DbContext.Roles.AsNoTracking().Where(r => r.Key == roleKey).Select(r => r.Id).SingleAsync();
        fixture.DbContext.UserRoles.Add(UserRole.Assign(userId, roleId, userType, null, DateTimeOffset.UtcNow));
        await fixture.DbContext.SaveChangesAsync();
        fixture.EvictAccess();
    }

    private static async Task<JsonElement> GetScopeAsync(IdentitySpecFixture fixture, string uid)
    {
        var response = await fixture.SendAsync(HttpMethod.Get, ScopePath, fixture.CreateToken(uid));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
