namespace ScrapGo.Core.Shared.Kernel.Quickbase;

/// <summary>
/// Runs Quickbase queries through the Postgres query cache (cache-aside).
/// </summary>
/// <remarks>
/// A contract in Shared.Kernel, implemented by the QuickbaseEngine module, so
/// proxy modules (Suppliers, Freight, ...) query Quickbase through it without
/// referencing QuickbaseEngine's projects, like <c>IUserContext</c>.
/// This service is authorization-agnostic: it runs every query with the
/// platform's single Quickbase credential, and cache entries are shared by
/// all callers. Callers must enforce the requesting user's
/// application/table access before calling it.
/// </remarks>
public interface IQuickbaseQueryService
{
    /// <summary>
    /// Returns the cached response when one younger than the TTL exists.
    /// Otherwise it queries Quickbase, stores the response, and returns it.
    /// </summary>
    /// <param name="forceRefresh">Skips the cache read (never the write), e.g. right after this app changed the table.</param>
    /// <exception cref="QuickbaseApiException">
    /// Quickbase failed and no cached entry could be served in its place.
    /// </exception>
    Task<QuickbaseQueryResult> QueryAsync(QuickbaseQuery query, bool forceRefresh = false, CancellationToken cancellationToken = default);
}

/// <param name="ResponseJson">The Quickbase response body verbatim (<c>data</c>, <c>fields</c>, <c>metadata</c>).</param>
/// <param name="Source">Where the response came from.</param>
/// <param name="FetchedAt">When the response was fetched from Quickbase: now on a miss, earlier on a hit.</param>
public sealed record QuickbaseQueryResult(string ResponseJson, QuickbaseResultSource Source, DateTimeOffset FetchedAt);

public enum QuickbaseResultSource
{
    /// <summary>Served from a cache entry younger than the TTL.</summary>
    Cache,

    /// <summary>Fetched from Quickbase just now, and cached.</summary>
    Quickbase,

    /// <summary>
    /// Quickbase failed, so an expired cache entry was served instead
    /// (stale-if-error). Only when <c>Quickbase:QueryCache:ServeStaleOnError</c> is on.
    /// </summary>
    StaleCache,
}
