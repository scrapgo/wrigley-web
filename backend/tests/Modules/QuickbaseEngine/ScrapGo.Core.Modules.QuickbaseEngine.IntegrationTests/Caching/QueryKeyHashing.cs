// The cache key: deterministic, and different whenever the answer could differ.
namespace ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests.Caching;

public class QueryKeyHashing
{
    private const string Realm = "scrapgo.quickbase.com";
    private static readonly QuickbaseQuery Query = new("bloads001", Select: [3, 6], Where: "{6.EX.'Open'}");

    [Fact]
    public void Equal_queries_hash_identically() =>
        Assert.Equal(QueryKey.For(Realm, Query).Hash, QueryKey.For(Realm, Query with { Select = [3, 6] }).Hash);

    [Fact]
    public void The_hash_is_lower_case_hex_sha256() =>
        Assert.Matches("^[0-9a-f]{64}$", QueryKey.For(Realm, Query).Hash);

    [Fact]
    public void Realm_hostname_case_does_not_matter() =>
        Assert.Equal(QueryKey.For(Realm, Query).Hash, QueryKey.For(Realm.ToUpperInvariant(), Query).Hash);

    public static TheoryData<string, QuickbaseQuery> DifferentAnswers => new()
    {
        { "other realm", Query },
        { Realm, Query with { TableId = "bloads002" } },
        { Realm, Query with { Select = [6, 3] } },
        { Realm, Query with { Where = "{6.EX.'Closed'}" } },
        { Realm, Query with { SortBy = [new QuickbaseSort(6)] } },
        { Realm, Query with { Top = 10 } },
    };

    [Theory]
    [MemberData(nameof(DifferentAnswers))]
    public void Anything_that_changes_the_answer_changes_the_hash(string realm, QuickbaseQuery query) =>
        Assert.NotEqual(QueryKey.For(Realm, Query).Hash, QueryKey.For(realm, query).Hash);
}
