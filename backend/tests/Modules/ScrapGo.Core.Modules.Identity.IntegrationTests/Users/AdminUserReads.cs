// B14: GET /api/admin/users and /api/admin/users/{id}, gated on users.view.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class AdminUserReads
{
    public class Given_a_user_reader(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_users_pages_by_id_with_the_total()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var seeded = new List<int>();
            for (var i = 0; i < 3; i++)
            {
                seeded.Add((await fixture.SeedUserAsync($"page{i}@paged-users.example")).UserId);
            }

            var response = await fixture.SendAsync(
                HttpMethod.Get, "/api/admin/users?search=paged-users.example&page=2&pageSize=2", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
            Assert.Equal(2, body.GetProperty("page").GetInt32());
            Assert.Equal([seeded[2]], body.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("id").GetInt32()));
        }

        [Fact]
        public async Task Get_users_filters_by_search_and_status()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            await fixture.SeedUserAsync("active@filtered-users.example");
            var (_, disabledId) = await fixture.SeedUserAsync("disabled@filtered-users.example");
            var disabled = await fixture.DbContext.Users.SingleAsync(u => u.Id == disabledId);
            disabled.Disable(DateTimeOffset.UtcNow);
            await fixture.DbContext.SaveChangesAsync();

            var response = await fixture.SendAsync(
                HttpMethod.Get, "/api/admin/users?search=FILTERED-users&status=disabled", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
            Assert.Equal(disabledId, item.GetProperty("id").GetInt32());
            Assert.Equal("Disabled", item.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Get_users_lists_each_users_role_keys()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var (_, userId) = await fixture.SeedUserAsync("staff@roles-listed.example", UserType.Internal);
            var scrapGoUser = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Key == DefaultRoleNames.ScrapGoUser).Select(r => r.Id).SingleAsync();
            await fixture.AssignRoleAsync(userId, scrapGoUser);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/users?search=roles-listed", token);

            var item = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
            Assert.Equal([DefaultRoleNames.ScrapGoUser], item.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        [Fact]
        public async Task Get_users_filters_by_type()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var (_, internalId) = await fixture.SeedUserAsync("staff@typed-users.example", UserType.Internal);
            await fixture.SeedUserAsync("seller@typed-users.example");

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/users?search=typed-users&type=internal", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
            Assert.Equal(internalId, item.GetProperty("id").GetInt32());
            Assert.Equal("Internal", item.GetProperty("userType").GetString());
        }

        [Fact]
        public async Task Get_user_returns_the_detail_with_roles()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var (_, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.GrantPermissionsAsync(userId, SpecPermissions.Alpha);

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/users/{userId}", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(userId, body.GetProperty("id").GetInt32());
            var role = Assert.Single(body.GetProperty("roles").EnumerateArray());
            Assert.Equal(roleId, role.GetProperty("id").GetInt32());
            Assert.StartsWith("spec_role_", role.GetProperty("key").GetString());
            Assert.Equal(0, body.GetProperty("supplierRecordIds").GetArrayLength());
        }

        [Fact]
        public async Task Get_user_returns_an_external_users_supplier_links()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var (_, userId) = await fixture.SeedUserAsync($"seller-{Guid.NewGuid():N}@acme.example");
            fixture.DbContext.UserSuppliers.AddRange(
                UserSupplier.Link(userId, 17511, null, DateTimeOffset.UtcNow),
                UserSupplier.Link(userId, 9583, null, DateTimeOffset.UtcNow));
            await fixture.DbContext.SaveChangesAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/users/{userId}", token);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("External", body.GetProperty("userType").GetString());
            Assert.Equal([9583, 17511], body.GetProperty("supplierRecordIds").EnumerateArray().Select(i => i.GetInt32()));
        }

        [Fact]
        public async Task Get_user_for_an_unknown_id_returns_four_hundred_four()
        {
            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/users/999999", await PlatformReaderTokenAsync(fixture));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("page=0")]
        [InlineData("pageSize=101")]
        [InlineData("status=Banned")]
        [InlineData("status=1")]
        [InlineData("type=Partner")]
        public async Task Get_users_with_an_invalid_query_returns_four_hundred_with_reason_invalid_request(string query)
        {
            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/users?{query}", await PlatformReaderTokenAsync(fixture));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // site_admin holds users.view.
    public class Given_a_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_users_is_allowed()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Key == DefaultRoleNames.SiteAdmin)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/users", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_an_authenticated_user_without_users_view(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/admin/users")]
        [InlineData("/api/admin/users/1")]
        public async Task The_admin_reads_return_four_hundred_three(string path)
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, path, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/admin/users")]
        [InlineData("/api/admin/users/1")]
        public async Task The_admin_reads_return_four_hundred_one(string path)
        {
            var response = await fixture.Client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>A fresh user holding users.view.</summary>
    private static async Task<string> PlatformReaderTokenAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync("reader@platform.example");
        await fixture.GrantPermissionsAsync(userId, Permissions.UsersView);

        return fixture.CreateToken(uid);
    }
}
