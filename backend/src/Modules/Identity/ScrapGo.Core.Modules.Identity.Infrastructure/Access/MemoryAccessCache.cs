using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using ScrapGo.Core.Modules.Identity.Application.Access;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Access;

/// <summary>
/// <see cref="IAccessCache"/> over <see cref="IMemoryCache"/>: entries keyed by
/// UID, expiring after <see cref="Ttl"/>. Singleton, per instance; other
/// instances catch up within the TTL.
/// </summary>
/// <remarks>
/// <see cref="EvictUser"/> works by user id, so the cache remembers which UID
/// each cached user id belongs to. <see cref="EvictAll"/> cancels a token every
/// entry depends on, then starts a new one.
/// </remarks>
public sealed class MemoryAccessCache(IMemoryCache cache) : IAccessCache
{
    /// <summary>The agreed revocation target (ACCESS-MODEL.md section 8).</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<int, string> _uidByUserId = new();
    private CancellationTokenSource _generation = new();

    public async Task<AccessContext?> GetOrLoadAsync(
        string identityPlatformUid, Func<CancellationToken, Task<AccessContext?>> load, CancellationToken cancellationToken)
    {
        var key = Key(identityPlatformUid);
        if (cache.TryGetValue(key, out AccessContext? cached) && cached is not null)
        {
            return cached;
        }

        // Taken before loading, so an EvictAll during the load expires this entry too.
        var generation = Volatile.Read(ref _generation);
        var context = await load(cancellationToken);
        if (context is null)
        {
            return null;
        }

        _uidByUserId[context.UserId] = identityPlatformUid;
        cache.Set(key, context, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(Ttl)
            .AddExpirationToken(new CancellationChangeToken(generation.Token)));

        return context;
    }

    public void EvictUser(int userId)
    {
        if (_uidByUserId.TryRemove(userId, out var uid))
        {
            cache.Remove(Key(uid));
        }
    }

    public void EvictAll()
    {
        var previous = Interlocked.Exchange(ref _generation, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
        _uidByUserId.Clear();
    }

    private static string Key(string identityPlatformUid) => $"access:{identityPlatformUid}";
}
