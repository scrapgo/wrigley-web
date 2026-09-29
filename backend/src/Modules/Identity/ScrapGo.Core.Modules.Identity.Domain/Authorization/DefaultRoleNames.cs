namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Names of the built-in, platform-scoped roles seeded by migration. This is
/// the only place a role name may appear as a literal: every other access
/// decision goes through resolved permissions, never a role-name check.
/// </summary>
public static class DefaultRoleNames
{
    /// <summary>
    /// Granted, per organization, to whoever creates that organization.
    /// Lets the holder manage that organization's custom roles.
    /// </summary>
    public const string OrganizationAdministrator = "OrganizationAdministrator";
}
