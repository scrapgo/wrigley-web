import { useAuth } from './useAuth'

/** Permission keys that open the admin area. */
const ADMIN_PERMISSIONS = ['users.view', 'users.invite', 'roles.view', 'features.view', 'audit.view']

/**
 * Admin gate for the portal: shows the admin area to callers holding any
 * admin permission. Replaced by `useAccess()` in a later task.
 */
export function useAdminAccess() {
    const { user, isLoading } = useAuth()

    const held = new Set(user?.permissions.flatMap((scope) => scope.permissions) ?? [])

    return {
        isAdmin: ADMIN_PERMISSIONS.some((permission) => held.has(permission)),
        user,
        isLoading,
    }
}
