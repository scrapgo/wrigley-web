// STORY-015, B15: the permission catalog is seeded by migration and read-only
// over HTTP (GET /api/admin/permissions, grouped by feature, roles.view).
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class PermissionCatalogSeed
{
    private const string PermissionsPath = "api/admin/permissions";

    public class Given_the_migration_has_applied(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_permissions_table_holds_exactly_the_catalog() =>
            Assert.Equal(
                SpecPermissions.CatalogRows.Order(StringComparer.Ordinal),
                (await fixture.DbContext.Permissions.Select(p => p.Key).ToListAsync()).Order(StringComparer.Ordinal));

        [Fact]
        public async Task Get_permissions_returns_the_whole_catalog_grouped_by_feature()
        {
            var (uid, _) = await fixture.SeedSiteAdminAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/{PermissionsPath}", fixture.CreateToken(uid));
            response.EnsureSuccessStatusCode();

            var groups = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            var keys = groups.SelectMany(g => g.GetProperty("permissions").EnumerateArray()).Select(p => p.GetProperty("key").GetString()!);
            Assert.Equal(SpecPermissions.CatalogRows.Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));

            var users = Assert.Single(groups, g => g.GetProperty("featureKey").GetString() == FeatureKeys.Users);
            Assert.Equal(
                [Permissions.UsersInvite, Permissions.UsersManage, Permissions.UsersView],
                users.GetProperty("permissions").EnumerateArray().Select(p => p.GetProperty("key").GetString()));
            Assert.All(users.GetProperty("permissions").EnumerateArray(), p => Assert.Equal("Internal", p.GetProperty("audience").GetString()));
        }

        // Sad path, kept separate.

        [Fact]
        public async Task A_caller_without_roles_view_gets_four_hundred_three()
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/{PermissionsPath}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Proven by inspecting the real route table, not by trusting a comment.
    public class Given_the_route_table_after_startup(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public void No_write_route_is_registered_under_api_admin_permissions(string method) =>
            Assert.Empty(RoutesUnder(fixture, PermissionsPath, method));

        // V1 roles are fixed: they can't be created or deleted, only re-mapped.
        [Theory]
        [InlineData("POST")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        public void No_create_or_delete_route_is_registered_under_api_admin_roles(string method) =>
            Assert.Empty(RoutesUnder(fixture, "api/admin/roles", method));

        [Fact]
        public void The_only_roles_write_route_sets_permissions() =>
            Assert.Equal(
                ["api/admin/roles/{id:int}/permissions"],
                RoutesUnder(fixture, "api/admin/roles", "PUT").Select(e => e.RoutePattern.RawText!.TrimStart('/')));
    }

    private static IEnumerable<RouteEndpoint> RoutesUnder(IdentitySpecFixture fixture, string prefix, string method) =>
        fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);
}
