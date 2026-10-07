// ADMIN-API-STATUS Task 5: platform-wide user reads, gated on User.Read at platform scope.
using System.Net.Http.Json;
using System.Text.Json;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class AdminUserReads
{
    public class Given_a_platform_user_reader(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
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
                HttpMethod.Get, "/api/users?search=paged-users.example&page=2&pageSize=2", token);

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
                HttpMethod.Get, "/api/users?search=FILTERED-users&status=disabled", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
            Assert.Equal(disabledId, item.GetProperty("id").GetInt32());
            Assert.Equal("Disabled", item.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Get_user_returns_the_detail_with_roles_and_their_organization()
        {
            var token = await PlatformReaderTokenAsync(fixture);
            var (_, userId, organizationId) = await fixture.SeedMemberAsync();
            var orgRoleId = await fixture.GrantPermissionsAsync(userId, organizationId, SpecPermissions.Alpha);

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/users/{userId}", token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(userId, body.GetProperty("id").GetInt32());
            var role = Assert.Single(body.GetProperty("roles").EnumerateArray());
            Assert.Equal(orgRoleId, role.GetProperty("roleId").GetInt32());
            Assert.Equal(organizationId, role.GetProperty("organizationId").GetInt32());
        }

        [Fact]
        public async Task Get_user_for_an_unknown_id_returns_four_hundred_four()
        {
            var response = await fixture.SendAsync(HttpMethod.Get, "/api/users/999999", await PlatformReaderTokenAsync(fixture));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("page=0")]
        [InlineData("pageSize=101")]
        [InlineData("status=Banned")]
        [InlineData("status=1")]
        public async Task Get_users_with_an_invalid_query_returns_four_hundred_with_reason_invalid_request(string query)
        {
            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/users?{query}", await PlatformReaderTokenAsync(fixture));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    // The bootstrapped PlatformAdministrator holds User.Read at platform scope.
    public class Given_the_bootstrapped_platform_administrator(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Get_users_is_allowed()
        {
            var (uid, userId) = await fixture.SeedUserAsync();
            var roleId = await fixture.DbContext.Roles
                .Where(r => r.Name == DefaultRoleNames.PlatformAdministrator && r.OrganizationId == null)
                .Select(r => r.Id)
                .SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId, organizationId: null);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/users", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Sad paths, kept separate.

    public class Given_an_authenticated_user_without_user_read(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/users")]
        [InlineData("/api/users/1")]
        public async Task The_admin_reads_return_four_hundred_three(string path)
        {
            var (uid, _) = await fixture.SeedUserAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, path, fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // Org isolation: User.Read held inside an organization (every org admin
    // has it) is not platform-wide user access.
    public class Given_an_org_admin_holding_user_read_only_in_their_org(IdentitySpecFixture fixture)
        : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/users")]
        [InlineData("/api/users/{0}")]
        public async Task The_platform_reads_return_four_hundred_three(string pathFormat)
        {
            var (uid, _, organizationId) = await fixture.SeedOrganizationAdministratorAsync();
            var (_, colleagueId) = await fixture.SeedUserAsync();
            await fixture.SeedMembershipAsync(colleagueId, organizationId);

            var response = await fixture.SendAsync(HttpMethod.Get, string.Format(pathFormat, colleagueId), fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_an_unauthenticated_request(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("/api/users")]
        [InlineData("/api/users/1")]
        public async Task The_admin_reads_return_four_hundred_one(string path)
        {
            var response = await fixture.Client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>A fresh user holding User.Read at platform scope.</summary>
    private static async Task<string> PlatformReaderTokenAsync(IdentitySpecFixture fixture)
    {
        var (uid, userId) = await fixture.SeedUserAsync("reader@platform.example");
        await fixture.GrantPermissionsAsync(userId, organizationId: null, Permissions.UserRead);

        return fixture.CreateToken(uid);
    }
}
