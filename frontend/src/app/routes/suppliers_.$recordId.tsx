import { createFileRoute, redirect } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { SupplierDetailPanel } from '../components/suppliers/supplier-detail-panel'

export const Route = createFileRoute('/suppliers_/$recordId')({
    component: SupplierDetailComponent,
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

function SupplierDetailComponent() {
    const { recordId } = Route.useLoaderData()

    return (
        <AppShell
            active="suppliers"
            title="Supplier Details"
            subtitle="Full supplier record from the Quickbase Suppliers table."
        >
            <SupplierDetailPanel recordId={recordId} />
        </AppShell>
    )
}
