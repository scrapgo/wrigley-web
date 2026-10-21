import { useCallback, useMemo } from 'react'

import { useAuth } from './useAuth'

/**
 * The caller's access, from GET /api/me. UI checks permission keys only, never
 * role names; the API enforces the same keys.
 */
export function useAccess() {
    const { me, isLoading } = useAuth()

    const permissions = useMemo(() => new Set(me?.permissions ?? []), [me])
    const features = useMemo(() => new Set(me?.features ?? []), [me])

    /** True when the caller holds at least one of the permission keys. */
    const can = useCallback((...anyOf: string[]) => anyOf.some((key) => permissions.has(key)), [permissions])

    /** True when the feature is on for the caller. */
    const isOn = useCallback((feature: string) => features.has(feature), [features])

    return { me, can, isOn, isLoading }
}
