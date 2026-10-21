import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { Construction } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { RequirePermission } from '../components/access'
import { UsersPanel } from '../components/admin/users-panel'
import { EmptyState } from '../components/ui/empty-state'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs'
import { useAccess } from '../hooks/useAccess'
import { ADMIN_PERMISSIONS, ADMIN_TABS, NAV, type AdminTab } from '../lib/nav'

export const Route = createFileRoute('/admin')({
    component: () => (
        <RequirePermission anyOf={ADMIN_PERMISSIONS}>
            <AdminComponent />
        </RequirePermission>
    ),
    validateSearch: (search: Record<string, unknown>): { tab?: AdminTab } => ({
        tab: ADMIN_TABS.find((tab) => tab === search.tab),
    }),
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

/** Each tab's content. Tabs whose screens aren't rebuilt yet show a placeholder. */
const PANELS: Partial<Record<AdminTab, () => React.ReactNode>> = {
    users: () => <UsersPanel />,
}

function AdminComponent() {
    const { can } = useAccess()
    const { tab: requested } = Route.useSearch()
    const navigate = useNavigate({ from: Route.fullPath })

    // The same nav entries as the sidebar, so a tab shows exactly when its menu entry does.
    const tabs = NAV.filter((item) => item.tab && can(...item.anyOf))
    const active = tabs.find((item) => item.tab === requested)?.tab ?? tabs[0]?.tab

    return (
        <AppShell active={active ?? 'admin'} title="Administration" subtitle="Users, invitations, roles, features and the audit log.">
            <Tabs value={active} onValueChange={(tab) => navigate({ search: { tab: tab as AdminTab } })}>
                <TabsList className="flex-wrap">
                    {tabs.map((item) => {
                        const Icon = item.icon
                        return (
                            <TabsTrigger key={item.key} value={item.tab!}>
                                <Icon className="h-4 w-4" />
                                {item.label}
                            </TabsTrigger>
                        )
                    })}
                </TabsList>
                {tabs.map((item) => (
                    <TabsContent key={item.key} value={item.tab!}>
                        {PANELS[item.tab!]?.() ?? (
                            <EmptyState
                                icon={Construction}
                                title={`${item.label} is being rebuilt`}
                                description="This screen is moving to the new access model and will be back shortly."
                            />
                        )}
                    </TabsContent>
                ))}
            </Tabs>
        </AppShell>
    )
}
