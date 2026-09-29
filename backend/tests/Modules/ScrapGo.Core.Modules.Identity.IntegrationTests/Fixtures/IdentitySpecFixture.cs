using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;
using ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;
using ScrapGo.Core.Shared.Infrastructure.Audit;
using Testcontainers.PostgreSql;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Shared arrangement for the Identity specs. It drives the real deployed
/// entry point (<see cref="WebApplicationFactory{Program}"/>) against:
/// <list type="bullet">
/// <item>a real Testcontainers Postgres, with both the <c>audit</c> and
/// <c>identity</c> migrations applied. It is not an in-memory provider,
/// because provisioning's ON CONFLICT path is Postgres-specific.</item>
/// <item>an in-process fake JWKS endpoint, with a token-minting helper signed
/// by the key it advertises.</item>
/// </list>
/// One instance per scenario class (<see cref="IClassFixture{TFixture}"/>), so
/// each scenario gets its own database and RSA key pair.
/// </summary>
/// <remarks>
/// Settings go in as environment variables set before the factory is built.
/// Program.cs reads <c>Gcip:ProjectId</c> and the connection string eagerly,
/// before <c>builder.Build()</c>, and a <c>WithWebHostBuilder</c>
/// configuration override would arrive too late. Hence the assembly-wide
/// <c>DisableTestParallelization</c>.
/// </remarks>
public class IdentitySpecFixture : IAsyncLifetime
{
    public const string ProjectId = "scrapgo-identity-spec-project";
    public static readonly string Issuer = $"https://securetoken.google.com/{ProjectId}";

    private const string SigningKeyId = "identity-spec-signing-key-1";

    private static readonly string[] EnvironmentVariables =
        ["ConnectionStrings__Default", "Gcip__ProjectId", "Gcip__JwksUri", "INTERNAL_HD_ALLOWLIST"];

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RSA _signingRsa = RSA.Create(2048);
    private readonly SigningCredentials _signingCredentials;
    private readonly Dictionary<string, string?> _previousEnvironment = [];

    private WebApplicationFactory<Program>? _factory;
    private IServiceScope? _scope;

    public IdentitySpecFixture()
    {
        _signingCredentials = new SigningCredentials(
            new RsaSecurityKey(_signingRsa) { KeyId = SigningKeyId }, SecurityAlgorithms.RsaSha256);
    }

    /// <summary><c>INTERNAL_HD_ALLOWLIST</c> for this fixture. Null (the default) leaves it unset.</summary>
    protected virtual string? HdAllowlist => null;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// A DbContext from the real DI container, for arranging rows and asserting
    /// on them. It is long-lived, so assert with <c>AsNoTracking()</c> to see
    /// what the API wrote rather than a stale tracked copy.
    /// </summary>
    public IdentityDbContext DbContext { get; private set; } = null!;

    /// <summary>The app's root service provider, e.g. to inspect the registered route table.</summary>
    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        foreach (var name in EnvironmentVariables)
        {
            _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _container.GetConnectionString());
        Environment.SetEnvironmentVariable("Gcip__ProjectId", ProjectId);

        // Never actually dialed: the named HttpClient's handler is replaced
        // below. Set to something JWKS-shaped for clarity in diagnostics.
        Environment.SetEnvironmentVariable("Gcip__JwksUri", "https://fake-gcip-jwks.invalid/keys");
        Environment.SetEnvironmentVariable("INTERNAL_HD_ALLOWLIST", HdAllowlist);

        var jwksJson = BuildJwksJson(_signingRsa, SigningKeyId);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                // Registered after the module's own AddHttpClient call for the
                // same name, so this handler wins. Safe here (unlike the config
                // above) because the signing-key provider resolves its client
                // lazily, after the host is built.
                services.AddHttpClient(GcipSigningKeyProvider.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new StaticJsonResponseHandler(jwksJson));

                // Test-only probe routes (Fixtures/ProbeController.cs),
                // registered here so they never exist in the production host.
                services.AddControllers().AddApplicationPart(typeof(IdentitySpecFixture).Assembly);
            }));

        _scope = _factory.Services.CreateScope();

        // The audit schema first: the identity context maps audit.audit_logs
        // but leaves creating it to AuditDbContext.
        await _scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();

        DbContext = _scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await DbContext.Database.MigrateAsync();

        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _scope?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        _signingRsa.Dispose();

        foreach (var (name, value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Mints a GCIP-shaped ID token signed with this fixture's key. It is
    /// correct by default; pass overrides to build a specific sad-path token.
    /// </summary>
    /// <param name="notBefore">
    /// Seeds both the <c>nbf</c> claim (when <paramref name="includeNbf"/>)
    /// and the default <c>iat</c>. Real GCIP tokens carry no <c>nbf</c>, so
    /// pass <c>includeNbf: false</c> to match that production shape.
    /// </param>
    /// <param name="includeIat">False mints a token with no <c>iat</c> at all, ignoring <paramref name="issuedAt"/>.</param>
    /// <param name="signInProvider">
    /// Emitted as a nested <c>firebase.sign_in_provider</c> JSON claim, the way
    /// GCIP does, so it round-trips as a real JSON object.
    /// </param>
    public string CreateToken(
        string uid = "spec-user-1",
        string? email = null,
        string? hd = null,
        DateTimeOffset? authTime = null,
        string? signInProvider = null,
        string? issuer = null,
        string? audience = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        DateTime? issuedAt = null,
        bool includeIat = true,
        bool includeNbf = true,
        bool tamperSignature = false,
        IEnumerable<(string Type, string Value)>? extraClaims = null)
    {
        var notBeforeInstant = notBefore ?? DateTime.UtcNow.AddMinutes(-1);
        var resolvedExpires = expires ?? DateTime.UtcNow.AddMinutes(30);

        // nbf/exp are added as plain claims rather than through the
        // JwtSecurityToken constructor, which rejects nbf > exp (IDX12401) and
        // would make the "nbf after exp" sad-path token impossible to mint.
        List<Claim> claims = [new("sub", uid)];

        if (includeIat)
        {
            claims.Add(EpochClaim(JwtRegisteredClaimNames.Iat, issuedAt ?? notBeforeInstant));
        }

        if (includeNbf)
        {
            claims.Add(EpochClaim(JwtRegisteredClaimNames.Nbf, notBeforeInstant));
        }

        claims.Add(EpochClaim(JwtRegisteredClaimNames.Exp, resolvedExpires));

        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        if (hd is not null)
        {
            claims.Add(new Claim("hd", hd));
        }

        if (authTime is not null)
        {
            claims.Add(EpochClaim("auth_time", authTime.Value.UtcDateTime));
        }

        if (signInProvider is not null)
        {
            claims.Add(new Claim(
                "firebase", JsonSerializer.Serialize(new { sign_in_provider = signInProvider }), JsonClaimValueTypes.Json));
        }

        // Lets a spec mint an attacker-shaped claim (e.g. a forged organizationId)
        // and prove production code never reads it.
        foreach (var (type, value) in extraClaims ?? [])
        {
            claims.Add(new Claim(type, value));
        }

        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? ProjectId,
            claims: claims,
            notBefore: null,
            expires: null,
            signingCredentials: _signingCredentials));

        return tamperSignature ? FlipLastSignatureByte(jwt) : jwt;
    }

    /// <summary>Sends an authenticated request, with an optional JSON body.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string bearerToken, object? body = null) =>
        Client.SendAsync(new HttpRequestMessage(method, path)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bearerToken) },
            Content = body is null ? null : JsonContent.Create(body),
        });

    public Task<HttpResponseMessage> SendUsersMeAsync(string bearerToken) =>
        Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/users/me")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bearerToken) },
        });

    /// <summary>The caller's session on the header, the independent re-auth token in the JSON body.</summary>
    public Task<HttpResponseMessage> SendLinkProviderAsync(string bearerToken, string? reauthIdToken) =>
        Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/users/me/linked-providers/link")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", bearerToken) },
            Content = JsonContent.Create(new { reauthIdToken }),
        });

    public static async Task<string?> ReadProblemReasonAsync(HttpResponseMessage response)
    {
        using var problemDetails = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return problemDetails.RootElement.GetProperty("reason").GetString();
    }

    private static Claim EpochClaim(string type, DateTime instant) =>
        new(type, EpochTime.GetIntDate(instant).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);

    private static string FlipLastSignatureByte(string jwt)
    {
        var segments = jwt.Split('.');
        var signatureBytes = Base64UrlEncoder.DecodeBytes(segments[2]);
        signatureBytes[^1] ^= 0xFF;
        segments[2] = Base64UrlEncoder.Encode(signatureBytes);

        return string.Join('.', segments);
    }

    private static string BuildJwksJson(RSA rsa, string kid)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        return $$"""
            {"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{kid}}","n":"{{Base64UrlEncoder.Encode(parameters.Modulus)}}","e":"{{Base64UrlEncoder.Encode(parameters.Exponent)}}"}]}
            """;
    }

    /// <summary>Stands in for Google's JWKS endpoint, serving a fixed document for every request.</summary>
    private sealed class StaticJsonResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };

            return Task.FromResult(response);
        }
    }
}

/// <summary><see cref="IdentitySpecFixture"/> with <c>INTERNAL_HD_ALLOWLIST=example.com</c>.</summary>
public sealed class AllowlistedHdIdentitySpecFixture : IdentitySpecFixture
{
    protected override string? HdAllowlist => "example.com";
}
