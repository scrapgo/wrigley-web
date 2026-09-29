using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Application.Queries;

/// <summary>
/// The cache identity of a query: its canonical JSON form and that form's
/// SHA-256. Canonical means a fixed property order (record declaration
/// order), no whitespace, and nulls omitted, so equal queries always produce
/// the same bytes.
/// </summary>
/// <remarks>
/// Select and sort lists keep their given order, because order changes what
/// Quickbase returns. Bump <see cref="Version"/> whenever the canonical shape
/// changes, which orphans every old key instead of mis-serving it.
/// </remarks>
public sealed record QueryKey(string Hash, string CanonicalJson)
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static QueryKey For(string realm, QuickbaseQuery query)
    {
        var canonicalJson = JsonSerializer.Serialize(
            new { v = Version, realm = realm.ToLowerInvariant(), query },
            CanonicalJsonOptions);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

        return new QueryKey(hash, canonicalJson);
    }
}
