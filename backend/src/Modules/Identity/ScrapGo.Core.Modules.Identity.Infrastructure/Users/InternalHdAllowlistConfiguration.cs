using Microsoft.Extensions.Configuration;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Users;

/// <summary>Reads <c>INTERNAL_HD_ALLOWLIST</c> from configuration, in either of its two forms.</summary>
public static class InternalHdAllowlistConfiguration
{
    /// <summary>
    /// A JSON array in appsettings (<c>["scrapgo.com"]</c>) binds as child keys
    /// (<c>INTERNAL_HD_ALLOWLIST:0</c>), for which the plain indexer returns null.
    /// The comma-separated string (the Cloud Run env var) sets the key's own
    /// value. Both can be present at once, e.g. an env var over
    /// <c>appsettings.Development.json</c>, so a non-blank string wins, as an
    /// env var overrides appsettings; otherwise the array's items are read.
    /// </summary>
    public static HashSet<string> Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(InternalHdAllowlistOptions.ConfigurationKey);
        return string.IsNullOrWhiteSpace(section.Value)
            ? InternalHdAllowlistOptions.Parse(section.GetChildren().Select(child => child.Value))
            : InternalHdAllowlistOptions.Parse(section.Value);
    }
}
