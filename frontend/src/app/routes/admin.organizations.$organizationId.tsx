import { createFileRoute, redirect } from '@tanstack/react-router'
import { Building2 } from 'lucide-react'

import { AppShell } from '../../components/app-shell'
import { OrganizationDetailPanel } from '../../components/admin/organization-detail-panel'

export const Route = createFileRoute('/admin/organizations/$organizationId')({
    component: OrganizationDetailComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
    loader: async ({ params }) => {
        const organizationId = parseInt(params.organizationId)
        if (isNaN(organizationId)) {
            throw redirect({ to: '/admin' })
        }
        return { organizationId }
    }
})

function OrganizationDetailComponent() {
    const { organizationId } = Route.useLoaderData()

    return (
        <AppShell
            active="admin"
            title="Organization Details"
            subtitle="Manage organization settings and members."
        >
            <OrganizationDetailPanel organizationId={organizationId} />
        </AppShell>
    )
}