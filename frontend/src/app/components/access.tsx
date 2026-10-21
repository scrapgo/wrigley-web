import type { ReactNode } from 'react'
import { Link, Navigate } from '@tanstack/react-router'
import { SearchX } from 'lucide-react'

import { useAccess } from '../hooks/useAccess'
import { Button } from './ui/button'
import { EmptyState } from './ui/empty-state'

/** Renders `children` only when the caller holds one of `anyOf`; otherwise `fallback`. */
export function Can({ anyOf, fallback = null, children }: { anyOf: string[]; fallback?: ReactNode; children: ReactNode }) {
    const { can } = useAccess()

    return <>{can(...anyOf) ? children : fallback}</>
}

/**
 * Route wrapper: renders the page only when the caller holds one of `anyOf`,
 * and a "not found" page otherwise, so a page the caller can't use looks like
 * one that doesn't exist (as the API's 404s do).
 */
export function RequirePermission({ anyOf, children }: { anyOf: string[]; children: ReactNode }) {
    const { can, me, isLoading } = useAccess()

    if (isLoading) {
        return null
    }

    if (me === null) {
        return <Navigate to="/login" />
    }

    return can(...anyOf) ? <>{children}</> : <NotFound />
}

export function NotFound() {
    return (
        <div className="flex min-h-screen items-center justify-center bg-steel-50 px-4">
            <EmptyState
                icon={SearchX}
                title="Page not found"
                description="This page doesn't exist, or you don't have access to it."
                action={
                    <Button asChild>
                        <Link to="/dashboard">Go to the dashboard</Link>
                    </Button>
                }
            />
        </div>
    )
}
