import { useAuth } from './useAuth'

/**
 * Admin gate for the portal.
 *
 * Shows admin features to all authenticated users. The backend still enforces
 * real authorization and returns 403s for unauthorized actions.
 */
export function useAdminAccess() {
    const { user, isLoading } = useAuth()

    return {
        isAdmin: user !== null,
        isLoading,
    }
}
