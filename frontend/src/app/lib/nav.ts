import type { ComponentType } from 'react'
import { LayoutDashboard, MailPlus, ScrollText, ShieldCheck, ToggleRight, UserCog, Users } from 'lucide-react'

/** The admin page's tabs, each a `?tab=` value on `/admin`. */
export const ADMIN_TABS = ['users', 'invitations', 'roles', 'features', 'audit'] as const

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
    { key: 'users', label: 'Users', icon: UserCog, section: 'Administration', to: '/admin', tab: 'users', anyOf: ['users.view'] },
    {
        key: 'invitations',
        label: 'Invitations',
        icon: MailPlus,
        section: 'Administration',
        to: '/admin',
        tab: 'invitations',
        anyOf: ['users.invite', 'users.view'],
    },
    { key: 'roles', label: 'Roles', icon: ShieldCheck, section: 'Administration', to: '/admin', tab: 'roles', anyOf: ['roles.view'] },
    {
        key: 'features',
        label: 'Features',
        icon: ToggleRight,
        section: 'Administration',
        to: '/admin',
        tab: 'features',
        anyOf: ['features.view'],
    },
    { key: 'audit', label: 'Audit log', icon: ScrollText, section: 'Administration', to: '/admin', tab: 'audit', anyOf: ['audit.view'] },
]

/** Every permission that opens some admin tab. */
export const ADMIN_PERMISSIONS = [...new Set(NAV.filter((item) => item.tab).flatMap((item) => item.anyOf))]
