// Failures are never cached; an expired entry may stand in for a failed refresh (stale-if-error).
namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Queries;

public class QuickbaseFailures
{
    private static readonly QuickbaseQuery Suppliers = new("bsupp0001", Select: [3, 8]);

    public class Given_quickbase_fails_and_nothing_is_cached(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_call_throws_with_the_status_and_nothing_is_cached()
        {
            fixture.Quickbase.FailWith = HttpStatusCode.ServiceUnavailable;

            var exception = await Assert.ThrowsAsync<QuickbaseApiException>(() => fixture.QueryAsync(Suppliers));

            Assert.Equal(503, exception.StatusCode);
            Assert.Empty(await fixture.CacheRowsAsync());
        }
    }

    public class Given_quickbase_is_rate_limiting_and_an_expired_entry_exists(QueryCacheSpecFixture fixture)
        : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_expired_entry_is_served_flagged_as_stale()
        {
            var original = await fixture.QueryAsync(Suppliers);
            fixture.Clock.Advance(TimeSpan.FromHours(1));
            fixture.Quickbase.FailWith = HttpStatusCode.TooManyRequests;

            var result = await fixture.QueryAsync(Suppliers);

            Assert.Equal(QuickbaseResultSource.StaleCache, result.Source);
            Assert.Equal(original.FetchedAt, result.FetchedAt);
            QueryCacheSpecFixture.AssertSameJson(original.ResponseJson, result.ResponseJson);
        }
    }

    public class Given_stale_on_error_is_disabled(NoStaleOnErrorQueryCacheSpecFixture fixture)
        : IClassFixture<NoStaleOnErrorQueryCacheSpecFixture>
    {
        [Fact]
        public async Task A_failed_refresh_throws_even_though_an_expired_entry_exists()
        {
            await fixture.QueryAsync(Suppliers);
            fixture.Clock.Advance(TimeSpan.FromHours(1));
            fixture.Quickbase.FailWith = HttpStatusCode.InternalServerError;

            var exception = await Assert.ThrowsAsync<QuickbaseApiException>(() => fixture.QueryAsync(Suppliers));

            Assert.Equal(500, exception.StatusCode);
        }
    }
}
