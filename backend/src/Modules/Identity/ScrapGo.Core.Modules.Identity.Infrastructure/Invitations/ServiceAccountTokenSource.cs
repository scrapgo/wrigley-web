using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Invitations;

/// <summary>An OAuth access token for the Gmail API, acting as the sender mailbox.</summary>
public interface IGmailAccessTokenSource
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Google's service-account flow with domain-wide delegation: a JWT signed with
/// the account's private key, naming the sender mailbox as <c>sub</c>, is
/// exchanged at the token endpoint for an hour-long access token. Singleton: the
/// token is cached until shortly before it expires.
/// </summary>
public sealed class ServiceAccountTokenSource(
    IHttpClientFactory httpClientFactory, IOptions<GmailOptions> options, TimeProvider timeProvider) : IGmailAccessTokenSource
{
    public const string HttpClientName = "gmail-oauth";

    public const string GmailSendScope = "https://www.googleapis.com/auth/gmail.send";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is null || timeProvider.GetUtcNow() >= _expiresAt)
            {
                (_token, var lifetime) = await RequestTokenAsync(cancellationToken);
                // Renew a minute early so a token never expires mid-request.
                _expiresAt = timeProvider.GetUtcNow().Add(lifetime - TimeSpan.FromMinutes(1));
            }

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string Token, TimeSpan Lifetime)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var key = JsonSerializer.Deserialize<ServiceAccountKey>(options.Value.ServiceAccountJson)
            ?? throw new InvalidOperationException("Invitations:Gmail:ServiceAccountJson is not a service account key.");

        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKey);
        var now = timeProvider.GetUtcNow();
        var assertion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = key.ClientEmail,
            Audience = key.TokenUri,
            Subject = new ClaimsIdentity([new Claim("sub", options.Value.Sender), new Claim("scope", GmailSendScope)]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = key.PrivateKeyId }, SecurityAlgorithms.RsaSha256),
        });

        using var response = await httpClientFactory.CreateClient(HttpClientName).PostAsync(
            key.TokenUri,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The body names the problem (e.g. unauthorized_client: delegation not granted) and holds no secret.
            throw new HttpRequestException(
                $"Google token endpoint returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}",
                inner: null,
                response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Google token endpoint returned no token.");

        return (token.AccessToken, TimeSpan.FromSeconds(token.ExpiresIn));
    }

    private sealed record ServiceAccountKey(
        [property: JsonPropertyName("client_email")] string ClientEmail,
        [property: JsonPropertyName("private_key")] string PrivateKey,
        [property: JsonPropertyName("private_key_id")] string? PrivateKeyId,
        [property: JsonPropertyName("token_uri")] string TokenUri);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
