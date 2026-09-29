namespace ScrapGo.Core.Modules.QuickbaseEngine.Domain.Caching;

/// <summary>
/// The last Quickbase response for one distinct query, keyed by
/// <see cref="QueryHash"/>, a SHA-256 of the query's canonical form. There is
/// one row per distinct query: a refresh overwrites the row rather than
/// appending a new one.
/// </summary>
/// <remarks>
/// Freshness is decided at read time against the configured TTL
/// (<see cref="IsFreshAt"/>), not stored as an expiry. Changing the TTL
/// therefore applies to rows that already exist.
/// </remarks>
public class QueryCache
{
    private QueryCache()
    {
    }

    public int Id { get; private set; }

    /// <summary>Lower-case hex SHA-256 of the canonical query. Unique.</summary>
    public string QueryHash { get; private set; } = string.Empty;

    /// <summary>The Quickbase table (<c>from</c>) the query targets. Lets a table's entries be found and purged together.</summary>
    public string TableId { get; private set; } = string.Empty;

    /// <summary>The canonical query that produced <see cref="QueryHash"/>, kept for diagnostics. Contains no credentials.</summary>
    public string RequestJson { get; private set; } = string.Empty;

    /// <summary>The raw Quickbase response body, returned verbatim on a cache hit.</summary>
    public string ResponseJson { get; private set; } = string.Empty;

    /// <summary>When <see cref="ResponseJson"/> was fetched from Quickbase.</summary>
    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static QueryCache Create(
        string queryHash, string tableId, string requestJson, string responseJson, DateTimeOffset fetchedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);

        return new QueryCache
        {
            QueryHash = queryHash,
            TableId = tableId,
            RequestJson = requestJson,
            ResponseJson = responseJson,
            FetchedAt = fetchedAt,
            CreatedAt = fetchedAt,
        };
    }

    /// <summary>True while the entry is strictly younger than <paramref name="ttl"/>.</summary>
    public bool IsFreshAt(DateTimeOffset now, TimeSpan ttl) => now - FetchedAt < ttl;
}
