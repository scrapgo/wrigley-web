import { createFileRoute, redirect } from '@tanstack/react-router'
import { Construction } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { EmptyState } from '../components/ui/empty-state'

export const Route = createFileRoute('/admin_/users/$userId')({
    component: UserDetailComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
    loader: async ({ params }) => {
        const userId = parseInt(params.userId)
        if (isNaN(userId)) {
            throw redirect({ to: '/admin' })
        }
        return { userId }
    },
})

function UserDetailComponent() {
    return (
        <AppShell active="admin" title="User" subtitle="Roles, supplier links and status.">
            <EmptyState
                icon={Construction}
                title="Admin is being rebuilt"
                description="User details are moving to the new access model and will be back shortly."
            />
        </AppShell>
    )
}
