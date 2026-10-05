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

        // Find the permission scope entry for the given organization
        const scopeEntry = user.permissions.find(p => p.organizationId === organizationId)

        // Check if the permission exists in that scope
        return scopeEntry?.permissions.includes(permission) ?? false
    }

    // Check if user has platform admin access (Admin.Access at platform scope)
    const isPlatformAdmin = can('Admin.Access', null)

    // Check if user has any organization-level admin access
    const hasOrgAdminAccess = user?.permissions.some(
        p => p.organizationId !== null &&
            (p.permissions.includes('User.Read') ||
                p.permissions.includes('Role.Read') ||
                p.permissions.includes('Organization.Update'))
    ) ?? false

    // Check if user holds a specific role
    const holdsRole = (roleId: number, organizationId: number | null) => {
        if (!user?.roles) return false

        return user.roles.some(
            r => r.roleId === roleId && r.organizationId === organizationId
        )
    }

    return {
        isAdmin: isPlatformAdmin || hasOrgAdminAccess,
        isPlatformAdmin,
        can,
        holdsRole,
        user,
        isLoading,
    }
}
