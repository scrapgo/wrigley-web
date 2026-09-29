// AGENTS.md: every request is checked against the UserContext before Quickbase data is accessed.
namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Queries;

public class QuickbaseAccessRequiresUserContext
{
    private static readonly QuickbaseQuery Loads = new("bloads900", Select: [3]);

    public class Given_an_unauthenticated_caller(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_query_is_refused_without_calling_quickbase_or_touching_the_cache()
        {
            fixture.User.IsAuthenticated = false;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.QueryAsync(Loads));

            Assert.Equal(0, fixture.Quickbase.CallCount);
            Assert.Empty(await fixture.CacheRowsAsync());
        }
    }

    // Cached rows are Quickbase data too: a warm cache must not leak to a caller who has since been disabled.
    public class Given_a_warm_cache_and_a_caller_who_is_no_longer_active(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_cached_result_is_not_served()
        {
            await fixture.QueryAsync(Loads);
            fixture.User.IsActive = false;

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.QueryAsync(Loads));

            Assert.Equal(1, fixture.Quickbase.CallCount);
        }
    }
}
