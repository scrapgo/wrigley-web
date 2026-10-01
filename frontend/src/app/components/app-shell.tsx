import { useState, type ReactNode } from "react"
import * as DropdownMenu from "@radix-ui/react-dropdown-menu"
import {
    LayoutDashboard,
    TrendingUp,
    Target,
    Truck,
    Users,
    MessageSquare,
    FileText,
    ShieldCheck,
    Settings,
    LogOut,
    Menu,
    X,
    Bell,
    Search,
    ChevronRight,
    ChevronsUpDown,
    Check,
    Building2,
} from "lucide-react"

import { cn } from "../lib/utils"
import { BrandLogo } from "./brand-logo"
import { Button } from "./ui/button"
import { useAuth } from "../hooks/useAuth"

export type NavKey =
    | "home"
    | "pricing"
    | "opportunities"
    | "loads"
    | "suppliers"
    | "messages"
    | "documents"
    | "admin"
    | "settings"

interface NavItem {
    key: NavKey
    label: string
    icon: React.ComponentType<{ className?: string }>
    badge?: string
}

interface NavSection {
    label: string
    items: NavItem[]
}

const NAV_SECTIONS: NavSection[] = [
    {
        label: "Workspace",
        items: [
            { key: "home", label: "Home", icon: LayoutDashboard },
            { key: "pricing", label: "Pricing", icon: TrendingUp },
            { key: "opportunities", label: "Opportunities", icon: Target, badge: "12" },
            { key: "loads", label: "Loads & Freight", icon: Truck, badge: "24" },
            { key: "suppliers", label: "Suppliers", icon: Users },
        ],
    },
    {
        label: "Engage",
        items: [
            { key: "messages", label: "Messages", icon: MessageSquare, badge: "3" },
            { key: "documents", label: "Documents", icon: FileText },
        ],
    },
    {
        label: "Administration",
        items: [
            { key: "admin", label: "Access & Roles", icon: ShieldCheck },
            { key: "settings", label: "Settings", icon: Settings },
        ],
    },
]

interface Workspace {
    id: string
    name: string
    kind: string
}

const WORKSPACES: Workspace[] = [
    { id: "scrapgo", name: "ScrapGo", kind: "Internal" },
    { id: "midwest", name: "Midwest Metals", kind: "Supplier" },
    { id: "greatlakes", name: "Great Lakes Carriers", kind: "Carrier" },
]

interface AppShellProps {
    active: NavKey
    title: string
    subtitle?: string
    actions?: ReactNode
    children: ReactNode
}

export function AppShell({ active, title, subtitle, actions, children }: AppShellProps) {
    const [mobileOpen, setMobileOpen] = useState(false)
    const [workspace, setWorkspace] = useState<Workspace>(WORKSPACES[0])
    const { user, logout } = useAuth()

    const initials = (user?.email || "SG")
        .split(/[\s@.]+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((s) => s[0]?.toUpperCase())
        .join("")

    return (
        <div className="min-h-screen bg-steel-50">
            {/* Desktop sidebar */}
            <aside className="fixed inset-y-0 left-0 z-40 hidden w-72 flex-col ink-gradient lg:flex">
                <SidebarContent
                    active={active}
                    workspace={workspace}
                    onWorkspaceChange={setWorkspace}
                />
            </aside>

            {/* Mobile drawer */}
            {mobileOpen && (
                <div className="fixed inset-0 z-50 lg:hidden">
                    <div
                        className="absolute inset-0 bg-ink-950/60 backdrop-blur-sm animate-fade-in"
                        onClick={() => setMobileOpen(false)}
                        aria-hidden
                    />
                    <aside className="absolute inset-y-0 left-0 flex w-72 max-w-[85%] flex-col ink-gradient shadow-elevated animate-rise">
                        <button
                            onClick={() => setMobileOpen(false)}
                            className="absolute right-3 top-4 z-10 rounded-lg p-2 text-steel-300 hover:bg-white/10 hover:text-white"
                            aria-label="Close navigation"
                        >
                            <X className="h-5 w-5" />
                        </button>
                        <SidebarContent
                            active={active}
                            workspace={workspace}
                            onWorkspaceChange={setWorkspace}
                            onNavigate={() => setMobileOpen(false)}
                        />
                    </aside>
                </div>
            )}

            {/* Main column */}
            <div className="lg:pl-72">
                {/* Top bar */}
                <header className="sticky top-0 z-30 border-b border-border glass">
                    <div className="flex h-16 items-center gap-3 px-4 sm:px-6 lg:px-8">
                        <button
                            onClick={() => setMobileOpen(true)}
                            className="rounded-lg p-2 text-ink-700 hover:bg-muted lg:hidden"
                            aria-label="Open navigation"
                        >
                            <Menu className="h-5 w-5" />
                        </button>

                        <div className="lg:hidden">
                            <BrandLogo size={30} withWordmark={false} />
                        </div>

                        <div className="relative hidden max-w-md flex-1 md:block">
                            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <input
                                type="search"
                                placeholder="Search loads, suppliers, opportunities…"
                                className="h-10 w-full rounded-lg border border-border bg-background/70 pl-9 pr-3 text-sm outline-none transition-colors placeholder:text-muted-foreground focus:border-brand-500 focus:ring-2 focus:ring-ring/25"
                            />
                        </div>

                        <div className="ml-auto flex items-center gap-2">
                            <button
                                className="relative rounded-lg p-2.5 text-ink-600 hover:bg-muted"
                                aria-label="Notifications"
                            >
                                <Bell className="h-5 w-5" />
                                <span className="absolute right-2 top-2 h-2 w-2 rounded-full bg-brand-500 ring-2 ring-card" />
                            </button>

                            <div className="hidden items-center gap-3 rounded-full border border-border bg-card py-1 pl-1 pr-3 sm:flex">
                                <span className="flex h-8 w-8 items-center justify-center rounded-full brand-gradient text-xs font-bold text-white">
                                    {initials || "SG"}
                                </span>
                                <span className="leading-tight">
                                    <span className="block text-xs font-semibold text-ink-900">
                                        {user?.email || "ScrapGo User"}
                                    </span>
                                    <span className="block text-[0.6875rem] text-muted-foreground">
                                        {user?.classification || "Member"}
                                    </span>
                                </span>
                            </div>

                            <Button
                                variant="ghost"
                                size="icon"
                                onClick={logout}
                                aria-label="Sign out"
                                className="text-ink-600"
                            >
                                <LogOut className="h-5 w-5" />
                            </Button>
                        </div>
                    </div>
                </header>

                {/* Page header */}
                <div className="border-b border-border bg-card">
                    <div className="flex flex-col gap-4 px-4 py-6 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
                        <div className="animate-rise">
                            <h1 className="text-2xl font-bold text-ink-900 sm:text-3xl">
                                {title}
                            </h1>
                            {subtitle && (
                                <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p>
                            )}
                        </div>
                        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
                    </div>
                </div>

                <main className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">{children}</main>
            </div>
        </div>
    )
}

function SidebarContent({
    active,
    workspace,
    onWorkspaceChange,
    onNavigate,
}: {
    active: NavKey
    workspace: Workspace
    onWorkspaceChange: (workspace: Workspace) => void
    onNavigate?: () => void
}) {
    return (
        <>
            <div className="flex h-16 items-center px-6">
                <BrandLogo inverted withTagline size={34} />
            </div>

            {/* Workspace switcher */}
            <div className="px-3 pb-2">
                <DropdownMenu.Root>
                    <DropdownMenu.Trigger asChild>
                        <button className="flex w-full items-center gap-3 rounded-xl border border-white/10 bg-white/5 px-3 py-2.5 text-left transition-colors hover:bg-white/10 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
                            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-brand-500/20 text-brand-300">
                                <Building2 className="h-4 w-4" />
                            </span>
                            <span className="min-w-0 flex-1 leading-tight">
                                <span className="block truncate text-sm font-semibold text-white">
                                    {workspace.name}
                                </span>
                                <span className="block text-[0.6875rem] text-steel-400">
                                    {workspace.kind} workspace
                                </span>
                            </span>
                            <ChevronsUpDown className="h-4 w-4 shrink-0 text-steel-400" />
                        </button>
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Portal>
                        <DropdownMenu.Content
                            align="start"
                            sideOffset={6}
                            className="z-50 w-[15rem] rounded-xl border border-border bg-popover p-1.5 text-popover-foreground shadow-elevated animate-fade-in"
                        >
                            <DropdownMenu.Label className="px-2.5 py-1.5 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted-foreground">
                                Switch workspace
                            </DropdownMenu.Label>
                            {WORKSPACES.map((ws) => (
                                <DropdownMenu.Item
                                    key={ws.id}
                                    onSelect={() => onWorkspaceChange(ws)}
                                    className="flex cursor-pointer items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm outline-none transition-colors data-[highlighted]:bg-muted"
                                >
                                    <span className="flex h-7 w-7 items-center justify-center rounded-md bg-brand-50 text-brand-600">
                                        <Building2 className="h-3.5 w-3.5" />
                                    </span>
                                    <span className="min-w-0 flex-1 leading-tight">
                                        <span className="block truncate font-medium text-ink-900">
                                            {ws.name}
                                        </span>
                                        <span className="block text-[0.6875rem] text-muted-foreground">
                                            {ws.kind}
                                        </span>
                                    </span>
                                    {ws.id === workspace.id && (
                                        <Check className="h-4 w-4 text-brand-600" />
                                    )}
                                </DropdownMenu.Item>
                            ))}
                        </DropdownMenu.Content>
                    </DropdownMenu.Portal>
                </DropdownMenu.Root>
            </div>

            <nav className="flex-1 space-y-5 overflow-y-auto px-3 py-3">
                {NAV_SECTIONS.map((section) => (
                    <div key={section.label} className="space-y-1">
                        <p className="px-3 pb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-steel-500">
                            {section.label}
                        </p>
                        {section.items.map((item) => {
                            const Icon = item.icon
                            const isActive = item.key === active
                            return (
                                <a
                                    key={item.key}
                                    href="#"
                                    onClick={(e) => {
                                        e.preventDefault()
                                        onNavigate?.()
                                    }}
                                    className={cn(
                                        "group flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
                                        isActive
                                            ? "bg-brand-500/15 text-white"
                                            : "text-steel-300 hover:bg-white/5 hover:text-white"
                                    )}
                                >
                                    <Icon
                                        className={cn(
                                            "h-5 w-5 shrink-0",
                                            isActive
                                                ? "text-brand-400"
                                                : "text-steel-400 group-hover:text-steel-200"
                                        )}
                                    />
                                    <span className="flex-1">{item.label}</span>
                                    {item.badge && (
                                        <span className="rounded-full bg-brand-500 px-2 py-0.5 text-[0.6875rem] font-bold text-white">
                                            {item.badge}
                                        </span>
                                    )}
                                    {isActive && <ChevronRight className="h-4 w-4 text-brand-400" />}
                                </a>
                            )
                        })}
                    </div>
                ))}
            </nav>

            <div className="border-t border-white/10 p-4">
                <div className="rounded-xl bg-white/5 p-4">
                    <p className="text-xs font-semibold text-white">Need a hand?</p>
                    <p className="mt-1 text-xs leading-relaxed text-steel-400">
                        Reach the ScrapGo team for logistics or pricing support.
                    </p>
                    <a
                        href="https://scrapgo.com/contact-us/"
                        target="_blank"
                        rel="noreferrer"
                        className="mt-3 inline-flex items-center gap-1 text-xs font-semibold text-brand-400 hover:text-brand-300"
                    >
                        Contact support <ChevronRight className="h-3.5 w-3.5" />
                    </a>
                </div>
            </div>
        </>
    )
}
