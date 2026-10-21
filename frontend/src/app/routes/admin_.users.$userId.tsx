import { createFileRoute, redirect } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { RequirePermission } from '../components/access'
import { UserDetailPanel } from '../components/admin/user-detail-panel'

export const Route = createFileRoute('/admin_/users/$userId')({
    component: () => (
        <RequirePermission anyOf={['users.view']}>
            <UserDetailComponent />
        </RequirePermission>
    ),
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
    const { userId } = Route.useLoaderData()

    return (
        <AppShell active="users" title="User" subtitle="Roles, supplier links and status.">
            <UserDetailPanel userId={userId} />
        </AppShell>
    )
}
