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
    /// ScrapGo staff with administration: Admin.Access, User.* and Role.*.
    /// The first one comes only from the explicit <c>bootstrap-platform-admin</c>
    /// command, never at sign-in.
    /// </summary>
    public const string PlatformAdministrator = "PlatformAdministrator";
}
