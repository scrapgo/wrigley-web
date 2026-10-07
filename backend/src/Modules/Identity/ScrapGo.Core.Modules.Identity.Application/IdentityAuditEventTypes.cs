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

    public const string UserDisabled = "user_disabled";

    public const string UserEnabled = "user_enabled";

    public const string RoleAssigned = "role_assigned";

    public const string RoleRevoked = "role_revoked";

    public const string OrganizationUpdated = "organization_updated";

    public const string MembershipAdded = "membership_added";

    public const string MembershipRemoved = "membership_removed";

    public const string ApplicationAssigned = "application_assigned";

    /// <summary>Metadata carries <c>revokedGrantCount</c>; each revoked grant has its own <c>access_revoked</c>.</summary>
    public const string ApplicationRemoved = "application_removed";

    public const string ModuleEnabled = "module_enabled";

    public const string ModuleDisabled = "module_disabled";

    /// <summary>An application role granted (or its expiry changed) at (organization, application) scope.</summary>
    public const string AccessGranted = "access_granted";

    /// <summary>An application grant revoked; metadata <c>reason</c> names a cascade (application_removed, membership_removed) when it wasn't direct.</summary>
    public const string AccessRevoked = "access_revoked";

    public const string CatalogApplicationChanged = "catalog_application_changed";

    public const string CatalogModuleChanged = "catalog_module_changed";

    /// <summary>A member asked for an application their organization doesn't have (or that is retired).</summary>
    public const string DeniedApplicationAccess = "denied_application_access";

    public const string InvitationCreated = "invitation_created";

    public const string InvitationAccepted = "invitation_accepted";

    public const string InvitationRevoked = "invitation_revoked";

    public const string OrganizationDeactivated = "organization_deactivated";

    public const string OrganizationReactivated = "organization_reactivated";

    /// <summary>A platform admin permanently deleted a deactivated organization. Its earlier audit entries are kept.</summary>
    public const string OrganizationDeleted = "organization_deleted";

    /// <summary>The <c>bootstrap-platform-admin</c> command granted the first PlatformAdministrator.</summary>
    public const string PlatformAdministratorBootstrapped = "platform_administrator_bootstrapped";
}
