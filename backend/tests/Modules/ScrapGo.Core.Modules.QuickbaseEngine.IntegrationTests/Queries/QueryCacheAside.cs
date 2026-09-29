// Cache-aside over Quickbase: serve fresh cache, otherwise fetch, store, return.
namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Queries;

public class QueryCacheAside
{
    private static readonly QuickbaseQuery OpenLoads = new(
        TableId: "bloads001",
        Select: [3, 6, 7],
        Where: "{6.EX.'Open'}",
        SortBy: [new QuickbaseSort(7, QuickbaseSortOrder.Descending)],
        Top: 100);

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    /// <summary>One query against an empty cache.</summary>
    public sealed class EmptyCacheFixture : QueryCacheSpecFixture
    {
        public QuickbaseQueryResult Result { get; private set; } = null!;

        protected override async Task ArrangeAsync() => Result = await QueryAsync(OpenLoads);
    }

    public class Given_an_empty_cache(EmptyCacheFixture fixture) : IClassFixture<EmptyCacheFixture>
    {
        [Fact]
        public void The_result_comes_from_quickbase()
        {
            Assert.Equal(QuickbaseResultSource.Quickbase, fixture.Result.Source);
            Assert.Equal(FakeQuickbaseApi.ResponseFor(1), fixture.Result.ResponseJson);
            Assert.Equal(fixture.Clock.GetUtcNow(), fixture.Result.FetchedAt);
        }

        [Fact]
        public async Task The_response_is_stored_under_the_query_hash_with_the_fetch_time()
        {
            var row = Assert.Single(await fixture.CacheRowsAsync());

            Assert.Equal(QueryKey.For(QueryCacheSpecFixture.Realm, OpenLoads).Hash, row.QueryHash);
            Assert.Equal(OpenLoads.TableId, row.TableId);
            Assert.Equal(fixture.Clock.GetUtcNow(), row.FetchedAt);
            QueryCacheSpecFixture.AssertSameJson(FakeQuickbaseApi.ResponseFor(1), row.ResponseJson);
        }

        [Fact]
        public void Quickbase_receives_an_authenticated_records_query_for_the_realm()
        {
            var request = Assert.Single(fixture.Quickbase.Requests);

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.quickbase.com/v1/records/query", request.Uri.ToString());
            Assert.Equal(QueryCacheSpecFixture.Realm, request.Headers["QB-Realm-Hostname"]);
            Assert.Equal($"QB-USER-TOKEN {QueryCacheSpecFixture.UserToken}", request.Headers["Authorization"]);
            QueryCacheSpecFixture.AssertSameJson(
                """{"from":"bloads001","select":[3,6,7],"where":"{6.EX.'Open'}","sortBy":[{"fieldId":7,"order":"DESC"}],"options":{"top":100}}""",
                request.Body!);
        }

        [Fact]
        public async Task The_stored_request_json_never_contains_the_user_token()
        {
            var row = Assert.Single(await fixture.CacheRowsAsync());

            Assert.DoesNotContain(QueryCacheSpecFixture.UserToken, row.RequestJson);
        }
    }

    public class Given_a_cached_response_younger_than_the_ttl(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task The_cached_json_is_returned_without_calling_quickbase()
        {
            var query = OpenLoads with { TableId = "byoung001" };
            var first = await fixture.QueryAsync(query);
            var callsAfterFirst = fixture.Quickbase.CallCount;
            fixture.Clock.Advance(DefaultTtl - TimeSpan.FromSeconds(1));

            var second = await fixture.QueryAsync(query);

            Assert.Equal(callsAfterFirst, fixture.Quickbase.CallCount);
            Assert.Equal(QuickbaseResultSource.Cache, second.Source);
            Assert.Equal(first.FetchedAt, second.FetchedAt);
            QueryCacheSpecFixture.AssertSameJson(first.ResponseJson, second.ResponseJson);
        }

        [Fact]
        public async Task An_equal_query_built_from_new_instances_hits_the_same_entry()
        {
            await fixture.QueryAsync(new QuickbaseQuery("bequal01", Select: [3, 6], Where: "{3.GT.0}"));
            var calls = fixture.Quickbase.CallCount;

            var result = await fixture.QueryAsync(new QuickbaseQuery("bequal01", Select: new List<int> { 3, 6 }, Where: "{3.GT.0}"));

            Assert.Equal(QuickbaseResultSource.Cache, result.Source);
            Assert.Equal(calls, fixture.Quickbase.CallCount);
        }
    }

    /// <summary>A cached query, then the same query again exactly one TTL later.</summary>
    public sealed class ExpiredEntryFixture : QueryCacheSpecFixture
    {
        public QuickbaseQueryResult Refreshed { get; private set; } = null!;

        protected override async Task ArrangeAsync()
        {
            await QueryAsync(OpenLoads);
            Clock.Advance(DefaultTtl);

            Refreshed = await QueryAsync(OpenLoads);
        }
    }

    public class Given_a_cached_response_that_has_reached_the_ttl(ExpiredEntryFixture fixture) : IClassFixture<ExpiredEntryFixture>
    {
        [Fact]
        public void Quickbase_is_queried_again_and_its_new_response_returned()
        {
            Assert.Equal(2, fixture.Quickbase.CallCount);
            Assert.Equal(QuickbaseResultSource.Quickbase, fixture.Refreshed.Source);
            Assert.Equal(FakeQuickbaseApi.ResponseFor(2), fixture.Refreshed.ResponseJson);
        }

        [Fact]
        public async Task The_same_row_is_updated_with_the_new_response_and_timestamp()
        {
            var row = Assert.Single(await fixture.CacheRowsAsync());

            Assert.Equal(fixture.Clock.GetUtcNow(), row.FetchedAt);
            QueryCacheSpecFixture.AssertSameJson(FakeQuickbaseApi.ResponseFor(2), row.ResponseJson);
        }
    }

    public class Given_a_configured_ttl_of_one_minute(OneMinuteTtlQueryCacheSpecFixture fixture)
        : IClassFixture<OneMinuteTtlQueryCacheSpecFixture>
    {
        [Fact]
        public async Task An_entry_older_than_one_minute_is_refreshed()
        {
            await fixture.QueryAsync(OpenLoads);
            fixture.Clock.Advance(TimeSpan.FromSeconds(61));

            var result = await fixture.QueryAsync(OpenLoads);

            Assert.Equal(QuickbaseResultSource.Quickbase, result.Source);
            Assert.Equal(2, fixture.Quickbase.CallCount);
        }
    }

    public class Given_a_fresh_entry_and_force_refresh(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task Quickbase_is_queried_and_the_entry_overwritten()
        {
            await fixture.QueryAsync(OpenLoads);
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));

            var result = await fixture.QueryAsync(OpenLoads, forceRefresh: true);

            Assert.Equal(QuickbaseResultSource.Quickbase, result.Source);
            var row = Assert.Single(await fixture.CacheRowsAsync());
            Assert.Equal(fixture.Clock.GetUtcNow(), row.FetchedAt);
        }
    }

    public class Given_queries_that_differ_only_in_their_filter(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task Each_gets_its_own_cache_entry()
        {
            await fixture.QueryAsync(OpenLoads);
            await fixture.QueryAsync(OpenLoads with { Where = "{6.EX.'Closed'}" });

            Assert.Equal(2, fixture.Quickbase.CallCount);
            Assert.Equal(2, (await fixture.CacheRowsAsync()).Select(r => r.QueryHash).Distinct().Count());
        }
    }

    // Race safety: concurrent misses for one query must neither fail on the
    // unique hash nor leave duplicate rows.
    public class Given_ten_concurrent_misses_for_the_same_query(QueryCacheSpecFixture fixture) : IClassFixture<QueryCacheSpecFixture>
    {
        [Fact]
        public async Task All_succeed_and_exactly_one_row_exists()
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.QueryAsync(OpenLoads)));

            Assert.All(results, r => Assert.NotEqual(QuickbaseResultSource.StaleCache, r.Source));
            Assert.Single(await fixture.CacheRowsAsync());
        }
    }
}
