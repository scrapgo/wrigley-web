using System.ComponentModel.DataAnnotations;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Quickbase;

/// <summary>
/// Retry and timeout settings for the Quickbase client's Polly pipeline. They
/// are bound from the same <c>Quickbase</c> section as <see cref="QuickbaseOptions"/>
/// (<c>Quickbase__Timeout</c>, <c>Quickbase__MaxRetryAttempts</c>,
/// <c>Quickbase__RetryBaseDelay</c>).
/// </summary>
/// <remarks>
/// Kept apart from the credentials on purpose. The resilience pipeline
/// validates its options at host startup, and reading
/// <see cref="QuickbaseOptions"/> there would force the credential check at
/// startup too, so no host could start without Quickbase secrets. These
/// settings all have safe defaults, so validating them at startup is fine.
/// </remarks>
public sealed class QuickbaseResilienceOptions
{
    /// <summary>
    /// Timeout for each attempt. A retried call can take up to
    /// (<see cref="MaxRetryAttempts"/> + 1) attempts plus backoff delays in
    /// total. Once retries are exhausted, a timeout counts as a failed call,
    /// so stale-if-error can serve the cached copy.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retries after the first attempt for transient failures: 408, 429 (which
    /// honors <c>Retry-After</c>), 5xx, network errors and attempt timeouts.
    /// Other 4xx responses are never retried.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>First retry delay. Later delays grow exponentially, with jitter.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "00:01:00")]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
