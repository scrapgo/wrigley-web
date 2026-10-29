import type { ComponentType } from 'react'
import { LayoutDashboard, ScrollText, ShieldCheck, UserCog, Users } from 'lucide-react'

/** The admin page's tabs, each a `?tab=` value on `/admin`. */
export const ADMIN_TABS = ['home', 'users', 'access', 'audit'] as const

export type AdminTab = (typeof ADMIN_TABS)[number]

export type NavKey = 'home' | 'suppliers' | AdminTab | 'admin' | 'settings'

export interface NavItem {
    key: NavKey
    label: string
    icon: ComponentType<{ className?: string }>
    section: 'Workspace' | 'Administration'
    to: '/dashboard' | '/suppliers' | '/admin'
    /** The `/admin` tab this entry opens. */
    tab?: AdminTab
    /** Shown to callers holding any of these permission keys. */
    anyOf: string[]
}

/** The app-shell menu: the single source of what each entry needs. */
export const NAV: NavItem[] = [
    { key: 'home', label: 'Dashboard', icon: LayoutDashboard, section: 'Workspace', to: '/dashboard', anyOf: ['dashboard.view'] },
    {
        key: 'suppliers',
        label: 'Suppliers',
        icon: Users,
        section: 'Workspace',
        to: '/suppliers',
        anyOf: ['suppliers.view_all', 'suppliers.view_own'],
    },
    {
        key: 'home',
        label: 'Home',
        icon: LayoutDashboard,
        section: 'Administration',
        to: '/admin',
        tab: 'home',
        // The admin landing summary; anyone who can reach admin can see it.
        anyOf: ['users.view', 'users.invite', 'roles.view', 'features.view', 'audit.view'],
    },
    {
        key: 'users',
        label: 'Users',
        icon: UserCog,
        section: 'Administration',
        to: '/admin',
        tab: 'users',
        // Whoever can see the user list, invite someone, or both.
        anyOf: ['users.view', 'users.invite'],
    },
    {
        key: 'access',
        label: 'Access & Features',
        icon: ShieldCheck,
        section: 'Administration',
        to: '/admin',
        tab: 'access',
        // Roles and features are both configuration; either permission opens the tab.
        anyOf: ['roles.view', 'roles.manage', 'features.view', 'features.manage'],
    },
    { key: 'audit', label: 'Audit log', icon: ScrollText, section: 'Administration', to: '/admin', tab: 'audit', anyOf: ['audit.view'] },
]

/** Every permission that opens some admin tab. */
export const ADMIN_PERMISSIONS = [...new Set(NAV.filter((item) => item.tab).flatMap((item) => item.anyOf))]
