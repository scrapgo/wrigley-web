// The Polly pipeline on the Quickbase typed client: retry transient failures, never permanent ones.
namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Caching;

public class QuickbaseRetries
{
    private static readonly QuickbaseQuery Carriers = new("bcarr0001", Select: [3, 9]);

    public class Given_quickbase_fails_transiently_twice_then_recovers(QueryCacheSpecFixture fixture)
        : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_query_succeeds_on_the_third_attempt_and_is_cached()
        {
            fixture.Quickbase.FailWith = HttpStatusCode.ServiceUnavailable;
            fixture.Quickbase.FailFirst = 2;

            var result = await fixture.QueryAsync(Carriers);

            Assert.Equal(QuickbaseResultSource.Quickbase, result.Source);
            Assert.Equal(3, fixture.Quickbase.CallCount);
            Assert.Equal(FakeQuickbaseApi.ResponseFor(3), result.ResponseJson);
            Assert.Single(await fixture.CacheRowsAsync());
        }
    }

    public class Given_quickbase_is_rate_limiting_then_recovers(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task A_429_is_retried()
        {
            fixture.Quickbase.FailWith = HttpStatusCode.TooManyRequests;
            fixture.Quickbase.FailFirst = 1;

            var result = await fixture.QueryAsync(Carriers);

            Assert.Equal(QuickbaseResultSource.Quickbase, result.Source);
            Assert.Equal(2, fixture.Quickbase.CallCount);
        }
    }

    // Sad paths, kept separate.

    public class Given_quickbase_keeps_failing(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task It_gives_up_after_the_initial_attempt_plus_three_retries()
        {
            fixture.Quickbase.FailWith = HttpStatusCode.BadGateway;

            var exception = await Assert.ThrowsAsync<QuickbaseApiException>(() => fixture.QueryAsync(Carriers));

            Assert.Equal(502, exception.StatusCode);
            Assert.Equal(4, fixture.Quickbase.CallCount);
        }
    }

    // A malformed query won't succeed on retry: retrying would only burn Quickbase's rate limit.
    public class Given_quickbase_rejects_the_request(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task A_400_is_not_retried()
        {
            fixture.Quickbase.FailWith = HttpStatusCode.BadRequest;

            var exception = await Assert.ThrowsAsync<QuickbaseApiException>(() => fixture.QueryAsync(Carriers));

            Assert.Equal(400, exception.StatusCode);
            Assert.Equal(1, fixture.Quickbase.CallCount);
        }
    }
}
