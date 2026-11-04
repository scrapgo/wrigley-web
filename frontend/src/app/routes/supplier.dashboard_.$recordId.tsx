import { createFileRoute, redirect } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { RequirePermission } from '../components/access'
import { SupplierDashboardPanel } from '../components/suppliers/supplier-dashboard-panel'

export const Route = createFileRoute('/supplier/dashboard_/$recordId')({
    component: () => (
        <RequirePermission anyOf={['suppliers.view_all', 'suppliers.view_own']}>
            <SupplierDashboardComponent />
        </RequirePermission>
    ),
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
    loader: async ({ params }) => {
        const recordId = parseInt(params.recordId)
        if (isNaN(recordId) || recordId < 1) {
            throw redirect({ to: '/suppliers' })
        }
        return { recordId }
    },
})

function SupplierDashboardComponent() {
    const { recordId } = Route.useLoaderData()

    return (
        <AppShell
            active="supplier-dashboard"
            title="Supplier Portal"
            subtitle="Your loads, payments, and quick actions — all in one place."
        >
            <SupplierDashboardPanel recordId={recordId} />
        </AppShell>
    )
}
