namespace ScrapGo.Core.Modules.Identity.Domain.Authorization;

/// <summary>
/// Names of the built-in, platform-defined roles seeded by migration. This is
/// the only place a role name may appear as a literal. Access decisions go
/// through resolved permissions, never a role-name check; the names exist only
/// so code can find the built-in rows to assign them.
/// </summary>
public static class DefaultRoleNames
{
    /// <summary>
    /// Granted, per organization, to whoever creates that organization. It
    /// holds every catalog permission except <see cref="Permissions.AdminAccess"/>,
    /// always scoped to that one organization.
    /// </summary>
    public const string OrganizationAdministrator = "OrganizationAdministrator";

    /// <summary>
    /// ScrapGo staff with platform-wide administration: Admin.Access, User.*
    /// and Role.*. It is only ever assigned at platform scope (no
    /// organization): first through the explicit <c>bootstrap-platform-admin</c>
    /// command, never at sign-in.
    /// </summary>
    public const string PlatformAdministrator = "PlatformAdministrator";
}
