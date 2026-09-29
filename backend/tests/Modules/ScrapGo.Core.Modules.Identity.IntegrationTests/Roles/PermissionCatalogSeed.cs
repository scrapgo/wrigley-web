// STORY-015: The permission catalog is seeded by migration and read-only over HTTP.
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Roles;

public class PermissionCatalogSeed
{
    private const string PermissionsPath = "api/permissions";

    public class Given_the_migration_has_applied(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task The_permissions_table_holds_exactly_the_catalog() =>
            Assert.Equal(
                Permissions.All.Order(),
                await fixture.DbContext.Permissions.Select(p => p.Name).OrderBy(n => n).ToListAsync());

        [Fact]
        public async Task Get_permissions_returns_the_whole_catalog()
        {
            var response = await fixture.SendAsync(HttpMethod.Get, $"/{PermissionsPath}", fixture.CreateToken(Guid.NewGuid().ToString()));
            response.EnsureSuccessStatusCode();

            var names = (await response.Content.ReadFromJsonAsync<List<NameOnly>>())!.Select(p => p.Name);

            Assert.Equal(Permissions.All.Order(), names);
        }

        private sealed record NameOnly(string Name);
    }

    // Proven by inspecting the real route table, not by trusting a comment.
    public class Given_the_route_table_after_startup(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public void No_write_route_is_registered_under_api_permissions(string method)
        {
            var writeRoutes = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith(PermissionsPath, StringComparison.OrdinalIgnoreCase) == true)
                .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

            Assert.Empty(writeRoutes);
        }
    }
}
