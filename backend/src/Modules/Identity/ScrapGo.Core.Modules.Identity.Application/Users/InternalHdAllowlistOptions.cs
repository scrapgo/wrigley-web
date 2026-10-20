namespace ScrapGo.Core.Modules.Identity.Application.Users;

/// <summary>
/// Hosted-domain (<c>hd</c> claim) values that classify a newly provisioned
/// user as <see cref="UserType.Internal"/>. Parsed once at startup
/// from the <c>INTERNAL_HD_ALLOWLIST</c> setting: a JSON array in appsettings
/// (<c>["scrapgo.com"]</c>) or a comma-separated string (the Cloud Run env var).
/// Empty (the default) means every user is classified External.
/// </summary>
public sealed class InternalHdAllowlistOptions
{
    public const string ConfigurationKey = "INTERNAL_HD_ALLOWLIST";

    /// <summary>Compared case-insensitively, since domain names are.</summary>
    public IReadOnlySet<string> HostedDomains { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool Contains(string? hostedDomain) =>
        hostedDomain is not null && HostedDomains.Contains(hostedDomain);

    public static HashSet<string> Parse(string? rawAllowlist) => Parse([rawAllowlist]);

    /// <summary>Every value may itself be comma-separated; blanks are dropped.</summary>
    public static HashSet<string> Parse(IEnumerable<string?> rawValues) =>
        new(
            rawValues.SelectMany(raw => (raw ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
            StringComparer.OrdinalIgnoreCase);
}
