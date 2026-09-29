namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Persistence;

public sealed class QueryCacheStore(QuickbaseDbContext dbContext) : IQueryCacheStore
{
    public const string QueryHashConstraint = "ux_query_caches_query_hash";

    public Task<QueryCache?> FindAsync(string queryHash, CancellationToken cancellationToken) =>
        dbContext.QueryCaches
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.QueryHash == queryHash, cancellationToken);

    /// <summary>
    /// A single <c>INSERT ... ON CONFLICT DO UPDATE</c>, rather than
    /// read-then-insert-or-update. Two requests that miss on the same query
    /// at the same time can't both insert (a unique violation) or lose an
    /// update: Postgres serializes them on the unique index.
    /// </summary>
    /// <remarks>
    /// The <c>WHERE</c> on the update keeps the newest response: a slow request
    /// that finishes last can't overwrite fresher data that a faster
    /// concurrent request already stored.
    /// </remarks>
    public Task UpsertAsync(QueryCache entry, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO quickbase.query_caches (query_hash, table_id, request_json, response_json, fetched_at, created_at)
            VALUES ({entry.QueryHash}, {entry.TableId}, CAST({entry.RequestJson} AS jsonb), CAST({entry.ResponseJson} AS jsonb), {entry.FetchedAt}, {entry.CreatedAt})
            ON CONFLICT (query_hash) DO UPDATE SET
                table_id = EXCLUDED.table_id,
                request_json = EXCLUDED.request_json,
                response_json = EXCLUDED.response_json,
                fetched_at = EXCLUDED.fetched_at
            WHERE query_caches.fetched_at < EXCLUDED.fetched_at
            """, cancellationToken);
}
