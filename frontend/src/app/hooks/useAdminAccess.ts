import { useAuth } from './useAuth'

/**
 * Admin gate for the portal.
 *
 * Shows admin features only to users with appropriate permissions.
 * Checks the caller's permissions from GET /api/users/me.
 */
export function useAdminAccess() {
    const { user, isLoading } = useAuth()

    // Helper to check if user has a specific permission in a given organization scope
    const can = (permission: string, organizationId: number | null) => {
        if (!user?.permissions) return false

        // The platform/organization scope entry, not an application scope in that organization
        const scopeEntry = user.permissions.find(p => p.organizationId === organizationId && p.applicationId == null)

        // Check if the permission exists in that scope
        return scopeEntry?.permissions.includes(permission) ?? false
    }

    // Application scope: a permission held for one application in one organization
    const canIn = (permission: string, organizationId: number, applicationId: number) =>
        user?.permissions.some(
            p => p.organizationId === organizationId && p.applicationId === applicationId && p.permissions.includes(permission)
        ) ?? false

    // Application administrator there: grants and revokes that application's access
    const isAppAdmin = (organizationId: number, applicationId: number) =>
        canIn('Application.ManageAccess', organizationId, applicationId)

    // Check if user has platform admin access (Admin.Access at platform scope)
    const isPlatformAdmin = can('Admin.Access', null)

    // Check if user has any organization-level admin access
    const hasOrgAdminAccess = user?.permissions.some(
        p => p.organizationId !== null &&
            (p.permissions.includes('User.Read') ||
                p.permissions.includes('Role.Read') ||
                p.permissions.includes('Organization.Update'))
    ) ?? false

    // Holds a platform role the current (non-Workspace) sign-in can't use. The
    // admin section stays visible so the page can ask for a Google sign-in.
    const workspaceSignInRequired = user?.workspaceSignInRequired ?? false

    // Check if user holds a specific role
    const holdsRole = (roleId: number, organizationId: number | null) => {
        if (!user?.roles) return false

        return user.roles.some(
            r => r.roleId === roleId && r.organizationId === organizationId
        )
    }

    return {
        isAdmin: isPlatformAdmin || hasOrgAdminAccess || workspaceSignInRequired,
        isPlatformAdmin,
        workspaceSignInRequired,
        can,
        canIn,
        isAppAdmin,
        holdsRole,
        user,
        isLoading,
    }
}
