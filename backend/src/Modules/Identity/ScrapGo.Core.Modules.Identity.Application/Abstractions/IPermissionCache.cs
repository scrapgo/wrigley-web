namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>
/// Caches each user's resolved permission set. Implementations must fail open
/// to the database: a cache outage degrades every lookup to "resolve from
/// Postgres", and never grants or denies on its own.
/// </summary>
/// <remarks>
/// Every mutation that changes what a user resolves to must call
/// <see cref="InvalidateAsync"/>.
/// </remarks>
public interface IPermissionCache
{
    /// <summary>The cached set, or null on a miss (or when the cache is unavailable).</summary>
    Task<IReadOnlySet<string>?> GetAsync(int userId, CancellationToken cancellationToken);

    Task SetAsync(int userId, IReadOnlySet<string> permissionNames, CancellationToken cancellationToken);

    Task InvalidateAsync(int userId, CancellationToken cancellationToken);
}
