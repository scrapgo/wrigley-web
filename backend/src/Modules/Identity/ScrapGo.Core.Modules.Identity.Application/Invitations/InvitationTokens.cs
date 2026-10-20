using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ScrapGo.Core.Modules.Identity.Application.Invitations;

/// <summary>
/// One-time invitation tokens (ACCESS-MODEL.md section 7): 32 random bytes,
/// base64url. Only the SHA-256 is stored, so a database leak yields no usable link.
/// </summary>
public static class InvitationTokens
{
    public const int TokenBytes = 32;

    /// <summary>How long an invitation (or a resent one) stays open.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <returns>The token for the accept link, and the hash to store.</returns>
    public static (string Token, string Hash) Generate()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

        return (token, Hash(token));
    }

    /// <summary>Lower-case hex SHA-256 of the token's UTF-8 bytes.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
