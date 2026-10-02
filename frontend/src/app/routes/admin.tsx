import { createFileRoute, redirect } from '@tanstack/react-router'
import { ShieldCheck } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs'
import { OrganizationsPanel } from '../components/admin/organizations-panel'
import { RolesPanel } from '../components/admin/roles-panel'
import { PermissionsPanel } from '../components/admin/permissions-panel'

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
        <AppShell
            active="admin"
            title="Access & Roles"
            subtitle="Manage organizations, roles, and the permission catalog."
        >
            <Tabs defaultValue="organizations">
                <TabsList>
                    <TabsTrigger value="organizations">Organizations</TabsTrigger>
                    <TabsTrigger value="roles">Roles</TabsTrigger>
                    <TabsTrigger value="permissions">
                        <ShieldCheck className="h-4 w-4" />
                        Permissions
                    </TabsTrigger>
                </TabsList>
                <TabsContent value="organizations">
                    <OrganizationsPanel />
                </TabsContent>
                <TabsContent value="roles">
                    <RolesPanel />
                </TabsContent>
                <TabsContent value="permissions">
                    <PermissionsPanel />
                </TabsContent>
            </Tabs>
        </AppShell>
    )
}
