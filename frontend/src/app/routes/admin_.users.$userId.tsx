import { createFileRoute, redirect } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { RoleAssignmentPanel } from '../components/admin/role-assignment-panel'

export const Route = createFileRoute('/admin_/users/$userId')({
    component: UserRoleManagementComponent,
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
    }
})

function UserRoleManagementComponent() {
    const { userId } = Route.useLoaderData()

    return (
        <AppShell
            active="admin"
            title="User Role Management"
            subtitle="Manage roles and permissions for this user."
        >
            <RoleAssignmentPanel userId={userId} />
        </AppShell>
    )
}