import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { AppWindow, ShieldAlert, ShieldCheck, Users } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs'
import { OrganizationsPanel } from '../components/admin/organizations-panel'
import { RolesPanel } from '../components/admin/roles-panel'
import { PermissionsPanel } from '../components/admin/permissions-panel'
import { UsersPanel } from '../components/admin/users-panel'
import { ApplicationsPanel } from '../components/admin/applications-panel'
import { Button } from '../components/ui/button'
import { useAdminAccess } from '../hooks/useAdminAccess'
import { useAuth } from '../hooks/useAuth'

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
    const { workspaceSignInRequired, isPlatformAdmin } = useAdminAccess()

    return (
        <AppShell
            active="admin"
            title="Access & Roles"
            subtitle="Manage organizations, roles, and the permission catalog."
        >
            {workspaceSignInRequired && <WorkspaceSignInNotice />}
            <Tabs defaultValue="organizations">
                <TabsList>
                    <TabsTrigger value="organizations">Organizations</TabsTrigger>
                    {isPlatformAdmin && (
                        <TabsTrigger value="applications">
                            <AppWindow className="h-4 w-4" />
                            Applications
                        </TabsTrigger>
                    )}
                    <TabsTrigger value="roles">Roles</TabsTrigger>
                    <TabsTrigger value="permissions">
                        <ShieldCheck className="h-4 w-4" />
                        Permissions
                    </TabsTrigger>
                    <TabsTrigger value="users">
                        <Users className="h-4 w-4" />
                        Users
                    </TabsTrigger>
                </TabsList>
                <TabsContent value="organizations">
                    <OrganizationsPanel />
                </TabsContent>
                {isPlatformAdmin && (
                    <TabsContent value="applications">
                        <ApplicationsPanel />
                    </TabsContent>
                )}
                <TabsContent value="roles">
                    <RolesPanel />
                </TabsContent>
                <TabsContent value="permissions">
                    <PermissionsPanel />
                </TabsContent>
                <TabsContent value="users">
                    <UsersPanel />
                </TabsContent>
            </Tabs>
        </AppShell>
    )
}

/**
 * Shown when the caller is a platform administrator but signed in without
 * their Google Workspace account (e.g. email/password), so the API withholds
 * platform access for this session.
 */
function WorkspaceSignInNotice() {
    const { logout } = useAuth()
    const navigate = useNavigate()

    const signInWithGoogle = () => {
        logout()
        navigate({ to: '/login' })
    }

    return (
        <div
            role="alert"
            className="mb-6 flex flex-col gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 sm:flex-row sm:items-center sm:justify-between"
        >
            <div className="flex items-start gap-2">
                <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                    <span className="font-semibold">Platform administration is unavailable for this sign-in.</span>{' '}
                    Sign out and use "Sign in with Google" with your Workspace account to manage the platform.
                </span>
            </div>
            <Button size="lg" variant="outline" className="shrink-0" onClick={signInWithGoogle}>
                Sign in with Google
            </Button>
        </div>
    )
}
