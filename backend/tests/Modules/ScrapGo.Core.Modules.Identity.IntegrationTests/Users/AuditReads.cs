// B17: GET /api/admin/audit (audit.view), read-only, newest first, filtered and paged.
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ScrapGo.Core.Shared.Infrastructure.Audit;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Users;

public class AuditReads
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public class Given_a_site_admin(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Rows_come_newest_first_with_the_actors_email_and_metadata()
        {
            var (uid, adminId) = await fixture.SeedSiteAdminAsync();
            var action = UniqueAction();
            await SeedAsync(fixture, (action, adminId, T0, """{"targetUserId":7}"""), (action, adminId, T0.AddHours(1), null));

            var body = await GetAsync(fixture, uid, $"?action={action}");

            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);
            Assert.True(items[0].GetProperty("eventTime").GetDateTimeOffset() > items[1].GetProperty("eventTime").GetDateTimeOffset());
            Assert.Equal(adminId, items[0].GetProperty("actorUserId").GetInt32());
            Assert.Equal("site-admin@scrapgo.example", items[0].GetProperty("actorEmail").GetString());
            Assert.Equal(action, items[1].GetProperty("action").GetString());
            Assert.Equal(7, items[1].GetProperty("metadata").GetProperty("targetUserId").GetInt32());
        }

        [Fact]
        public async Task The_actor_filter_returns_only_their_rows()
        {
            var (uid, adminId) = await fixture.SeedSiteAdminAsync();
            var (_, otherId) = await fixture.SeedUserAsync();
            var action = UniqueAction();
            await SeedAsync(fixture, (action, adminId, T0, null), (action, otherId, T0, null));

            var body = await GetAsync(fixture, uid, $"?action={action}&actor={otherId}");

            var item = Assert.Single(body.GetProperty("items").EnumerateArray());
            Assert.Equal(otherId, item.GetProperty("actorUserId").GetInt32());
        }

        [Fact]
        public async Task The_target_filter_matches_target_user_id_in_the_metadata()
        {
            var (uid, adminId) = await fixture.SeedSiteAdminAsync();
            var action = UniqueAction();
            await SeedAsync(
                fixture,
                (action, adminId, T0, """{"targetUserId":41}"""),
                (action, adminId, T0, """{"targetUserId":42}"""),
                (action, adminId, T0, """{"roleId":41}"""));

            var body = await GetAsync(fixture, uid, $"?action={action}&target=41");

            var item = Assert.Single(body.GetProperty("items").EnumerateArray());
            Assert.Equal(41, item.GetProperty("metadata").GetProperty("targetUserId").GetInt32());
        }

        [Fact]
        public async Task The_date_range_includes_from_and_excludes_to()
        {
            var (uid, adminId) = await fixture.SeedSiteAdminAsync();
            var action = UniqueAction();
            await SeedAsync(
                fixture,
                (action, adminId, T0.AddMinutes(-1), null),
                (action, adminId, T0, null),
                (action, adminId, T0.AddHours(1), null));

            var body = await GetAsync(fixture, uid, $"?action={action}&from={Uri.EscapeDataString(T0.ToString("o"))}&to={Uri.EscapeDataString(T0.AddHours(1).ToString("o"))}");

            var item = Assert.Single(body.GetProperty("items").EnumerateArray());
            Assert.Equal(T0, item.GetProperty("eventTime").GetDateTimeOffset());
        }

        [Fact]
        public async Task Pages_carry_the_total()
        {
            var (uid, adminId) = await fixture.SeedSiteAdminAsync();
            var action = UniqueAction();
            await SeedAsync(fixture, [.. Enumerable.Range(0, 3).Select(i => (action, (int?)adminId, T0.AddMinutes(i), (string?)null))]);

            var body = await GetAsync(fixture, uid, $"?action={action}&page=2&pageSize=2");

            Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
            var oldest = Assert.Single(body.GetProperty("items").EnumerateArray());
            Assert.Equal(T0, oldest.GetProperty("eventTime").GetDateTimeOffset());
        }

        // Sad paths, kept separate.

        [Theory]
        [InlineData("?page=0")]
        [InlineData("?pageSize=101")]
        [InlineData("?actor=0")]
        [InlineData("?from=2026-01-02T00:00:00Z&to=2026-01-01T00:00:00Z")]
        public async Task An_invalid_query_returns_four_hundred_with_reason_invalid_request(string query)
        {
            var (uid, _) = await fixture.SeedSiteAdminAsync();

            var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/audit{query}", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_request", await IdentitySpecFixture.ReadProblemReasonAsync(response));
        }
    }

    public class Given_a_scrapgo_user(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Fact]
        public async Task Reading_the_audit_log_returns_four_hundred_three()
        {
            var (uid, userId) = await fixture.SeedUserAsync($"staff-{Guid.NewGuid():N}@scrapgo.example", UserType.Internal);
            var roleId = await fixture.DbContext.Roles.AsNoTracking()
                .Where(r => r.Key == DefaultRoleNames.ScrapGoUser).Select(r => r.Id).SingleAsync();
            await fixture.AssignRoleAsync(userId, roleId);

            var response = await fixture.SendAsync(HttpMethod.Get, "/api/admin/audit", fixture.CreateToken(uid));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class Given_the_route_table_after_startup(IdentitySpecFixture fixture) : IClassFixture<IdentitySpecFixture>
    {
        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public void No_write_route_is_registered_under_api_admin_audit(string method)
        {
            var writeRoutes = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("api/admin/audit", StringComparison.OrdinalIgnoreCase) == true)
                .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

            Assert.Empty(writeRoutes);
        }
    }

    private static string UniqueAction() => $"spec.audit_{Guid.NewGuid():N}";

    private static async Task SeedAsync(
        IdentitySpecFixture fixture, params (string Action, int? ActorId, DateTimeOffset At, string? Metadata)[] rows)
    {
        fixture.DbContext.AuditLogs.AddRange(rows.Select(row => new AuditLog
        {
            EventType = row.Action,
            UserId = row.ActorId,
            EventTime = row.At,
            Metadata = row.Metadata,
        }));
        await fixture.DbContext.SaveChangesAsync();
    }

    private static async Task<JsonElement> GetAsync(IdentitySpecFixture fixture, string uid, string query)
    {
        var response = await fixture.SendAsync(HttpMethod.Get, $"/api/admin/audit{query}", fixture.CreateToken(uid));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
