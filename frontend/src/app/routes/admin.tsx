import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'

import { AppShell } from '../components/app-shell'
import { RequirePermission } from '../components/access'
import { AccessPanel } from '../components/admin/access-panel'
import { AuditPanel } from '../components/admin/audit-panel'
import { HomePanel } from '../components/admin/home-panel'
import { UsersPanel } from '../components/admin/users-panel'
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

/** Each tab's content. */
const PANELS: Record<AdminTab, () => React.ReactNode> = {
    home: () => <HomePanel />,
    users: () => <UsersPanel />,
    access: () => <AccessPanel />,
    audit: () => <AuditPanel />,
}

function AdminComponent() {
    const { can } = useAccess()
    const { tab: requested } = Route.useSearch()
    const navigate = useNavigate({ from: Route.fullPath })

    // The same nav entries as the sidebar, so a tab shows exactly when its menu entry does.
    const tabs = NAV.filter((item) => item.tab && can(...item.anyOf))
    const active = tabs.find((item) => item.tab === requested)?.tab ?? tabs[0]?.tab

    return (
        <AppShell active={active ?? 'admin'} title="Administration" subtitle="Users, roles, features and the audit log.">
            <Tabs value={active} onValueChange={(tab) => navigate({ search: { tab: tab as AdminTab } })}>
                {/* All tabs visible at all times — they wrap on mobile instead of hiding off-screen. Ultra-compact. */}
                <TabsList className="flex-wrap gap-0.5">
                    {tabs.map((item) => {
                        const Icon = item.icon
                        return (
                            <TabsTrigger key={item.key} value={item.tab!} className="whitespace-normal text-center px-1.5 py-1 gap-1">
                                <Icon className="h-3 w-3 mb-0" />
                                <span className="block text-[0.6rem] leading-none">{item.label}</span>
                            </TabsTrigger>
                        )
                    })}
                </TabsList>
                {tabs.map((item) => (
                    <TabsContent key={item.key} value={item.tab!}>
                        {PANELS[item.tab!]()}
                    </TabsContent>
                ))}
            </Tabs>
        </AppShell>
    )
}
