import { createFileRoute, redirect } from '@tanstack/react-router'
import { Construction } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { EmptyState } from '../components/ui/empty-state'

export const Route = createFileRoute('/admin')({
    component: AdminComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

function AdminComponent() {
    return (
        <AppShell active="admin" title="Administration" subtitle="Users, invitations, roles, features and the audit log.">
            <EmptyState
                icon={Construction}
                title="Admin is being rebuilt"
                description="The admin screens are moving to the new access model and will be back shortly."
            />
        </AppShell>
    )
}
