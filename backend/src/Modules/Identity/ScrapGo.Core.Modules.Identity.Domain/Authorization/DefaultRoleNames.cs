namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Keys of the system roles seeded by migration (ACCESS-MODEL.md section 2).
/// This is the only place a role key may appear as a literal. Access decisions
/// go through permissions, never a role check; the keys exist only so code can
/// find the seeded rows to assign them.
/// </summary>
public static class DefaultRoleNames
{
    /// <summary>A few ScrapGo staff. Internal. The first comes from <c>bootstrap-platform-admin</c>.</summary>
    public const string SiteAdmin = "site_admin";

    /// <summary>Every internal user, assigned on first sign-in. Internal.</summary>
    public const string ScrapGoUser = "scrapgo_user";

    /// <summary>Buyers, sellers, transporters. External; joins by invitation only.</summary>
    public const string User = "user";
}
