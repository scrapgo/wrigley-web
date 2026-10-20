namespace ScrapGo.Core.Modules.Identity.Application;

/// <summary>Single source of truth for the audit event types this module writes.</summary>
public static class IdentityAuditEventTypes
{
    /// <summary><c>POST /api/session</c> created the user (ACCESS-MODEL.md section 10).</summary>
    public const string UserProvisioned = "user.provisioned";

    /// <summary>A known, active user signed in (<c>POST /api/session</c>).</summary>
    public const string SessionStarted = "session.started";

    /// <summary>A request was refused for a missing permission (ACCESS-MODEL.md section 10).</summary>
    public const string AccessDenied = "access.denied";

    public const string UserDisabled = "user_disabled";

    public const string UserEnabled = "user_enabled";

    public const string RoleAssigned = "role_assigned";

    public const string RoleRevoked = "role_revoked";

    /// <summary>The <c>bootstrap-platform-admin</c> command granted the first PlatformAdministrator.</summary>
    public const string PlatformAdministratorBootstrapped = "platform_administrator_bootstrapped";
}
