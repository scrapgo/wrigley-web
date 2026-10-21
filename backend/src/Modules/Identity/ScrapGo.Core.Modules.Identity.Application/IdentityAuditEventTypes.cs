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

    public const string UserDisabled = "user.disabled";

    public const string UserEnabled = "user.enabled";

    /// <summary><c>PUT /api/admin/features/{id}</c> changed a feature's toggle (metadata: from, to).</summary>
    public const string FeatureStateChanged = "feature.state_changed";

    /// <summary><c>PUT /api/admin/features/{id}/targets</c> changed who a feature targets.</summary>
    public const string FeatureTargetsChanged = "feature.targets_changed";

    /// <summary><c>PUT /api/admin/roles/{id}/permissions</c> changed what a role grants (metadata: added and removed keys).</summary>
    public const string RolePermissionsChanged = "role.permissions_changed";

    /// <summary><c>PUT /api/admin/users/{id}/roles</c> changed the user's roles (metadata: added and removed keys).</summary>
    public const string UserRolesChanged = "user.roles_changed";

    /// <summary><c>PUT /api/admin/users/{id}/suppliers</c> changed the user's supplier links (metadata: added and removed Record IDs).</summary>
    public const string UserSuppliersChanged = "user.suppliers_changed";

    /// <summary>The <c>bootstrap-site-admin</c> command assigned site_admin.</summary>
    public const string SiteAdminBootstrapped = "site_admin.bootstrapped";
}
