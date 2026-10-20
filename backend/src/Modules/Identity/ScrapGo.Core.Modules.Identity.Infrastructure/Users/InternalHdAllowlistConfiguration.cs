using Microsoft.Extensions.Configuration;
using ScrapGo.Core.Modules.Identity.Application.Users;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Users;

/// <summary>Reads <c>INTERNAL_HD_ALLOWLIST</c> from configuration, in either of its two forms.</summary>
public static class InternalHdAllowlistConfiguration
{
    /// <summary>
    /// A JSON array in appsettings (<c>["scrapgo.com"]</c>) binds as child keys
    /// (<c>INTERNAL_HD_ALLOWLIST:0</c>), for which the plain indexer returns null,
    /// so the children are read when there are any. Otherwise the value is the
    /// comma-separated string the Cloud Run env var uses.
    /// </summary>
    public static HashSet<string> Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(InternalHdAllowlistOptions.ConfigurationKey);
        var children = section.GetChildren().ToList();
        return children.Count > 0
            ? InternalHdAllowlistOptions.Parse(children.Select(child => child.Value))
            : InternalHdAllowlistOptions.Parse(section.Value);
    }
}
