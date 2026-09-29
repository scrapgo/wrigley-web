namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Abstractions;

public interface IQueryCacheStore
{
    /// <summary>The entry for <paramref name="queryHash"/>, fresh or not, or null.</summary>
    Task<QueryCache?> FindAsync(string queryHash, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the entry, or overwrites the existing one for the same hash,
    /// atomically in the database, so concurrent misses for one query never
    /// collide. An older response never overwrites a newer one.
    /// </summary>
    Task UpsertAsync(QueryCache entry, CancellationToken cancellationToken);
}
