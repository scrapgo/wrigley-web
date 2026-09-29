namespace ScrapGo.Core.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Configuration for validating Google Cloud Identity Platform (GCIP) JWTs,
/// bound from the <c>Gcip</c> section (<c>Gcip__ProjectId</c>, <c>Gcip__JwksUri</c>).
/// </summary>
public sealed class GcipOptions
{
    public const string SectionName = "Gcip";

    /// <summary>
    /// The single GCIP project backing this deployment. It is both the expected
    /// <c>aud</c> claim and the source of the expected <c>iss</c>
    /// (<c>https://securetoken.google.com/{ProjectId}</c>). Required: startup
    /// fails fast if it is blank.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The JWKS endpoint GCIP tokens are signed against. Configurable so specs
    /// can point it at a fake instead of making real network calls.
    /// </summary>
    public string JwksUri { get; set; } =
        "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";

    public string Issuer => $"https://securetoken.google.com/{ProjectId}";
}
