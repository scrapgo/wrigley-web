namespace ScrapGo.Core.Modules.Identity.Application;

/// <summary>Single source of truth for the audit event types this module writes.</summary>
public static class IdentityAuditEventTypes
{
    /// <summary>First request for a UID created its user row.</summary>
    public const string UserProvisioned = "user_provisioned";

    /// <summary>An authenticated request from a disabled user was denied.</summary>
    public const string DeniedDisabledUser = "denied_disabled_user";

    public const string UserDisabled = "user_disabled";

    public const string UserEnabled = "user_enabled";

    public const string RoleAssigned = "role_assigned";

    public const string RoleRevoked = "role_revoked";

    /// <summary>The <c>bootstrap-platform-admin</c> command granted the first PlatformAdministrator.</summary>
    public const string PlatformAdministratorBootstrapped = "platform_administrator_bootstrapped";
}
