namespace ScrapGo.Core.Modules.Identity.Application;

/// <summary>Single source of truth for the audit event types this module writes.</summary>
public static class IdentityAuditEventTypes
{
    /// <summary>First request for a UID created its user row.</summary>
    public const string UserProvisioned = "user_provisioned";

    /// <summary>An authenticated request from a disabled user was denied.</summary>
    public const string DeniedDisabledUser = "denied_disabled_user";

    /// <summary>A user linked an additional sign-in provider.</summary>
    public const string ProviderLinked = "provider_linked";

    /// <summary>A user created an organization, becoming its OrganizationAdministrator.</summary>
    public const string OrganizationCreated = "organization_created";

    /// <summary>A request for an organization-scoped route from a caller with no active membership in it was denied.</summary>
    public const string DeniedCrossTenantAccess = "denied_cross_tenant_access";

    public const string RoleCreated = "role_created";

    public const string RoleUpdated = "role_updated";

    public const string RoleDeleted = "role_deleted";

    public const string RolePermissionAttached = "role_permission_attached";

    public const string RolePermissionDetached = "role_permission_detached";
}
