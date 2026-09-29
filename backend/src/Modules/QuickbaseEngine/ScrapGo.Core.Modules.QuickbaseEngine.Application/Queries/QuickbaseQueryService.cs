using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

/// <summary>
/// Cache-aside over Quickbase. A query hashes to a <see cref="QueryKey"/>.
/// A cache entry younger than the TTL is returned as is. Otherwise Quickbase
/// is queried, and the response is upserted with a new fetch time and
/// returned.
/// </summary>
/// <remarks>
/// <para>
/// Failures are never cached: only a successful Quickbase response is written.
/// </para>
/// <para>
/// Concurrent misses for the same query each call Quickbase (there's no
/// cross-instance request coalescing). Their writes are race-safe through the
/// store's atomic upsert, and the newest response wins.
/// </para>
/// </remarks>
public sealed class QuickbaseQueryService(
    IQuickbaseClient quickbase,
    IQueryCacheStore cache,
    IOptions<QuickbaseQueryCacheOptions> options,
    TimeProvider timeProvider,
    ILogger<QuickbaseQueryService> logger) : IQuickbaseQueryService
{
    public async Task<QuickbaseQueryResult> QueryAsync(
        QuickbaseQuery query, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.TableId);

        var key = QueryKey.For(quickbase.Realm, query);
        var ttl = options.Value.Ttl;

        var cached = await cache.FindAsync(key.Hash, cancellationToken);

        if (!forceRefresh && cached is not null && cached.IsFreshAt(timeProvider.GetUtcNow(), ttl))
        {
            return new QuickbaseQueryResult(cached.ResponseJson, QuickbaseResultSource.Cache, cached.FetchedAt);
        }

        string responseJson;
        try
        {
            responseJson = await quickbase.QueryRecordsAsync(query, cancellationToken);
        }
        catch (QuickbaseApiException ex) when (cached is not null && options.Value.ServeStaleOnError)
        {
            logger.LogWarning(
                ex,
                "Quickbase query on table {TableId} failed (status {StatusCode}); serving the cached response fetched at {FetchedAt}.",
                query.TableId, ex.StatusCode, cached.FetchedAt);

            return new QuickbaseQueryResult(cached.ResponseJson, QuickbaseResultSource.StaleCache, cached.FetchedAt);
        }

        // Stamped after the response arrived, so the TTL measures the data's age, not the request's start.
        var fetchedAt = timeProvider.GetUtcNow();
        await cache.UpsertAsync(QueryCache.Create(key.Hash, query.TableId, key.CanonicalJson, responseJson, fetchedAt), cancellationToken);

        return new QuickbaseQueryResult(responseJson, QuickbaseResultSource.Quickbase, fetchedAt);
    }
}
