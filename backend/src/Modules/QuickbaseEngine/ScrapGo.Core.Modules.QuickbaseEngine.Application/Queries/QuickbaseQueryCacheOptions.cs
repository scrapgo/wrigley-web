using System.ComponentModel.DataAnnotations;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

/// <summary>Bound from <c>Quickbase:QueryCache</c> (e.g. <c>Quickbase__QueryCache__Ttl=00:15:00</c>).</summary>
public sealed class QuickbaseQueryCacheOptions
{
    public const string SectionName = "Quickbase:QueryCache";

    /// <summary>How long a cached response is served without asking Quickbase. Defaults to 15 minutes.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// When Quickbase fails (outage, 429 rate limit, 5xx), serve an expired
    /// entry rather than failing the request. The result is flagged
    /// <see cref="QuickbaseResultSource.StaleCache"/>. On by default, because
    /// the cache exists precisely to shield callers from Quickbase's limits.
    /// </summary>
    public bool ServeStaleOnError { get; set; } = true;
}
