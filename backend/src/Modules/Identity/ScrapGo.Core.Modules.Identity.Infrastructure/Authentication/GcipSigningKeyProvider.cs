using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Resolves the RSA signing keys GCIP publishes for verifying ID token
/// signatures. GCIP serves no OpenID Connect discovery document at its
/// issuer, so this replaces the discovery-based ConfigurationManager the
/// OIDC handler would otherwise use.
/// </summary>
public interface IGcipSigningKeyProvider
{
    /// <summary>Returns the cached keys, first refreshing the JWKS document if the cache has expired.</summary>
    Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches GCIP's JWKS document and caches the keys in memory until the
/// response's <c>Cache-Control: max-age</c> expires. Registered as a
/// singleton so the cache is actually shared. The <see cref="HttpClient"/>
/// still comes from <c>IHttpClientFactory</c> (named <see cref="HttpClientName"/>)
/// so specs can substitute its handler.
/// </summary>
public sealed class GcipSigningKeyProvider(HttpClient httpClient, IOptions<GcipOptions> options) : IGcipSigningKeyProvider
{
    /// <summary>Specs replace this named client's primary handler with a fake JWKS server.</summary>
    public const string HttpClientName = "GcipJwks";

    /// <summary>Used when the response carries no usable max-age, so stale keys still expire eventually.</summary>
    private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
    {
        if (IsCacheValid())
        {
            return _cachedKeys;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check: another caller may have refreshed while we waited.
            if (IsCacheValid())
            {
                return _cachedKeys;
            }

            using var response = await httpClient.GetAsync(options.Value.JwksUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _cachedKeys = [.. new JsonWebKeySet(json).GetSigningKeys()];
            _cacheExpiresAt = DateTimeOffset.UtcNow + GetCacheDuration(response.Headers.CacheControl);

            return _cachedKeys;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsCacheValid() => _cachedKeys.Count > 0 && DateTimeOffset.UtcNow < _cacheExpiresAt;

    private static TimeSpan GetCacheDuration(CacheControlHeaderValue? cacheControl) =>
        cacheControl?.MaxAge is { } maxAge && maxAge > TimeSpan.Zero ? maxAge : FallbackCacheDuration;
}
