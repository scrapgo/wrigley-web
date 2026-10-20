namespace ScrapGo.Core.Modules.Identity.Application;

/// <summary>Single source of truth for the audit event types this module writes.</summary>
public static class IdentityAuditEventTypes
{
    /// <summary><c>POST /api/session</c> created the user (ACCESS-MODEL.md section 10).</summary>
    public const string UserProvisioned = "user.provisioned";

    /// <summary>A known, active user signed in (<c>POST /api/session</c>).</summary>
    public const string SessionStarted = "session.started";

    /// <summary><c>POST /api/session</c> accepted an invitation and created the user.</summary>
    public const string InvitationAccepted = "invitation.accepted";

    public const string InvitationCreated = "invitation.created";

    /// <summary>A new token was issued; the old link stopped working.</summary>
    public const string InvitationResent = "invitation.resent";

    public const string InvitationRevoked = "invitation.revoked";

    /// <summary>A request was refused for a missing permission (ACCESS-MODEL.md section 10).</summary>
    public const string AccessDenied = "access.denied";

    public const string UserDisabled = "user_disabled";

    public const string UserEnabled = "user_enabled";

    public const string RoleAssigned = "role_assigned";

    public const string RoleRevoked = "role_revoked";

    /// <summary>The <c>bootstrap-site-admin</c> command assigned site_admin.</summary>
    public const string SiteAdminBootstrapped = "site_admin.bootstrapped";
}
