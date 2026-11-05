import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { useEffect } from 'react'

import { AppShell } from '../components/app-shell'
import { RequirePermission } from '../components/access'
import { useSuppliers } from '../hooks/useSupplierQueries'
import { useAuth } from '../hooks/useAuth'

export const Route = createFileRoute('/supplier/dashboard')({
    component: () => (
        <RequirePermission anyOf={['suppliers.view_all', 'suppliers.view_own']}>
            <SupplierDashboardLanding />
        </RequirePermission>
    ),
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

function SupplierDashboardLanding() {
    const { me } = useAuth()
    const navigate = useNavigate()
    const { data, isLoading } = useSuppliers('', 0, 100)

    useEffect(() => {
        if (isLoading || !data) {
            return
        }

        const user = me?.user
        const isExternal = user?.userType === 'External'
        const suppliers = data.items

        if (suppliers.length === 0) {
            navigate({ to: '/suppliers' })
            return
        }

        if (isExternal || suppliers.length === 1) {
            // External users see only their linked suppliers; internal users with one supplier go straight there.
            navigate({ to: `/supplier/dashboard/${suppliers[0].recordId}` })
        } else {
            // Internal users with multiple suppliers: go to the suppliers list to pick one.
            navigate({ to: '/suppliers' })
        }
    }, [data, isLoading, me, navigate])

    return (
        <AppShell
            active="supplier-dashboard"
            title="Supplier Portal"
            subtitle="Loading your supplier portal..."
        >
            <div className="flex min-h-screen items-center justify-center bg-steel-50 px-4">
                <div className="max-w-md rounded-xl border border-border bg-card p-8 text-center shadow-card">
                    <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-600 mx-auto mb-4">
                        <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                strokeWidth={2}
                                d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4"
                            />
                        </svg>
                    </div>
                    <h2 className="text-lg font-bold text-ink-900">Loading Supplier Portal</h2>
                    <p className="mt-2 text-sm text-muted-foreground">
                        Resolving your supplier dashboard…
                    </p>
                </div>
            </div>
        </AppShell>
    )
}
