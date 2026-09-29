using System.ComponentModel.DataAnnotations;

namespace ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure.Quickbase;

/// <summary>
/// Connection settings for the Quickbase REST API, bound from the
/// <c>Quickbase</c> section (<c>Quickbase__RealmHostname</c>,
/// <c>Quickbase__UserToken</c>, ...).
/// </summary>
/// <remarks>
/// Validated when the client is first used rather than at startup, so a host
/// or test that never touches Quickbase doesn't need credentials. Supply
/// <see cref="UserToken"/> from Secret Manager in deployed environments, and
/// never put it in appsettings or logs.
/// </remarks>
public sealed class QuickbaseOptions
{
    public const string SectionName = "Quickbase";

    /// <summary>The realm hostname sent as <c>QB-Realm-Hostname</c>, e.g. <c>scrapgo.quickbase.com</c>.</summary>
    [Required]
    public string RealmHostname { get; set; } = string.Empty;

    /// <summary>A Quickbase user token, sent as <c>Authorization: QB-USER-TOKEN …</c>. A secret.</summary>
    [Required]
    public string UserToken { get; set; } = string.Empty;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.quickbase.com/v1/";

    /// <summary>Sent as <c>User-Agent</c>. Quickbase asks integrations to identify themselves.</summary>
    public string UserAgent { get; set; } = "ScrapGo.Core.Api";
}
