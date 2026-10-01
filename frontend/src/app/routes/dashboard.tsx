import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import {
    Truck,
    TrendingUp,
    Target,
    Users,
    DollarSign,
    ArrowUpRight,
    Plus,
    FileText,
    MapPin,
    CheckCircle2,
    Clock,
    Package,
    Sparkles,
    MessageSquare,
    ShieldCheck,
    Gauge,
    CalendarClock,
    Building2,
} from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import { Badge } from '../components/ui/badge'
import { Skeleton } from '../components/ui/skeleton'
import {
    StatCard,
    MarketChart,
    ProgressRing,
    PriceTicker,
    type Stat,
    type TickerItem,
} from '../components/dashboard-widgets'
import { cn } from '../lib/utils'
import { useAuth } from '../hooks/useAuth'

export const Route = createFileRoute('/dashboard')({
    component: DashboardComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

const STATS: Stat[] = [
    {
        title: 'Active Loads',
        value: '24',
        delta: '+6 this week',
        trend: 'up',
        icon: Truck,
        tone: 'text-brand-600 bg-brand-50',
        spark: [12, 14, 13, 16, 18, 17, 20, 22, 21, 24],
    },
    {
        title: 'Scrap Index',
        value: 'Healthy',
        delta: '+2.4%',
        trend: 'up',
        icon: TrendingUp,
        tone: 'text-emerald-600 bg-emerald-50',
        spark: [38, 42, 40, 46, 44, 52, 50, 58, 62, 68],
    },
    {
        title: 'Open Opportunities',
        value: '12',
        delta: '+4 today',
        trend: 'up',
        icon: Target,
        tone: 'text-violet-600 bg-violet-50',
        spark: [4, 5, 6, 5, 7, 8, 9, 8, 10, 12],
    },
    {
        title: 'Pending Inquiries',
        value: '8',
        delta: '-3 today',
        trend: 'down',
        icon: Users,
        tone: 'text-amber-600 bg-amber-50',
        spark: [14, 13, 12, 13, 11, 10, 11, 9, 10, 8],
    },
]

const TICKER: TickerItem[] = [
    { symbol: 'HMS', name: '80:20', price: '$412/GT', change: '+2.4%', trend: 'up' },
    { symbol: 'SHRED', name: 'Shredded', price: '$438/GT', change: '+1.1%', trend: 'up' },
    { symbol: 'ALUM', name: 'Aluminum Wheels', price: '$0.72/lb', change: '-0.8%', trend: 'down' },
    { symbol: 'CU', name: 'Copper #2', price: '$3.18/lb', change: '+3.6%', trend: 'up' },
    { symbol: 'CARS', name: 'Crushed Cars', price: '$268/GT', change: '+0.9%', trend: 'up' },
    { symbol: 'BRASS', name: 'Yellow Brass', price: '$2.05/lb', change: '-1.2%', trend: 'down' },
]

const OPPORTUNITIES = [
    {
        id: 'OPP-2041',
        material: 'HMS 80:20',
        seller: 'Midwest Metals',
        location: 'Chicago, IL',
        value: '$18,400',
        status: 'Qualified',
    },
    {
        id: 'OPP-2042',
        material: 'Aluminum Wheels',
        seller: 'Great Lakes Auto',
        location: 'Gary, IN',
        value: '$9,120',
        status: 'New',
    },
    {
        id: 'OPP-2043',
        material: 'Crushed Cars',
        seller: 'Toledo Salvage',
        location: 'Toledo, OH',
        value: '$22,750',
        status: 'Contacted',
    },
    {
        id: 'OPP-2044',
        material: 'Copper #2',
        seller: 'Brew City Recycling',
        location: 'Milwaukee, WI',
        value: '$31,600',
        status: 'Qualified',
    },
]

const STATUS_VARIANT: Record<string, 'success' | 'warning' | 'neutral' | 'default'> = {
    Qualified: 'success',
    New: 'default',
    Contacted: 'warning',
}

const ACTIVITY = [
    {
        icon: Truck,
        text: 'Load #12345 assigned to Carrier ABC',
        user: 'John Smith',
        time: '2h ago',
        tone: 'text-brand-600 bg-brand-50',
    },
    {
        icon: CheckCircle2,
        text: 'Supplier registration approved — Midwest Metals',
        user: 'Sarah Johnson',
        time: '5h ago',
        tone: 'text-emerald-600 bg-emerald-50',
    },
    {
        icon: DollarSign,
        text: 'Pricing updated for Steel category',
        user: 'Michael Brown',
        time: '1d ago',
        tone: 'text-amber-600 bg-amber-50',
    },
    {
        icon: Package,
        text: 'New opportunity published — #1 HMS (80:20)',
        user: 'Dana Lee',
        time: '1d ago',
        tone: 'text-violet-600 bg-violet-50',
    },
]

const APPS = [
    { icon: TrendingUp, label: 'Pricing Index', tone: 'text-brand-600 bg-brand-50' },
    { icon: Target, label: 'Opportunities', tone: 'text-violet-600 bg-violet-50' },
    { icon: Truck, label: 'Loads & Freight', tone: 'text-emerald-600 bg-emerald-50' },
    { icon: Users, label: 'Suppliers', tone: 'text-amber-600 bg-amber-50' },
    { icon: MessageSquare, label: 'Messages', tone: 'text-sky-600 bg-sky-50' },
    { icon: ShieldCheck, label: 'Access & Roles', tone: 'text-rose-600 bg-rose-50' },
]

function DashboardComponent() {
    const [loading, setLoading] = useState(true)
    const { isAuthenticated, isLoading } = useAuth()
    const navigate = useNavigate()

    useEffect(() => {
        const timer = setTimeout(() => setLoading(false), 900)
        return () => clearTimeout(timer)
    }, [])

    // If the stored token turns out to be invalid (expired, disabled user),
    // the AuthProvider clears it — send the user back to sign in.
    useEffect(() => {
        if (!isLoading && !isAuthenticated) {
            navigate({ to: '/login' })
        }
    }, [isLoading, isAuthenticated, navigate])

    return (
        <AppShell
            active="home"
            title="Home"
            subtitle="Your ScrapGo operations, unified in one place."
            actions={
                <>
                    <Button variant="outline" size="sm">
                        <FileText className="h-4 w-4" />
                        Reports
                    </Button>
                    <Button size="sm">
                        <Plus className="h-4 w-4" />
                        New Opportunity
                    </Button>
                </>
            }
        >
            {/* Welcome hero */}
            <section className="relative overflow-hidden rounded-2xl ink-gradient p-6 sm:p-8">
                <div className="absolute inset-0 grid-pattern opacity-40" aria-hidden />
                <div
                    className="absolute -right-16 -top-24 h-72 w-72 rounded-full brand-glow blur-2xl"
                    aria-hidden
                />
                <div className="relative flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
                    <div className="max-w-xl animate-rise">
                        <p className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-semibold text-brand-300">
                            <Sparkles className="h-3.5 w-3.5" />
                            Downstream Portal
                        </p>
                        <h2 className="mt-4 text-2xl font-bold leading-tight text-white sm:text-3xl">
                            Welcome back to ScrapGo.
                        </h2>
                        <p className="mt-2 text-sm leading-relaxed text-steel-300">
                            Real-time pricing, qualified opportunities, and freight — all in one
                            proactive line of communication with your team.
                        </p>
                        <div className="mt-5 flex flex-wrap items-center gap-3">
                            <Button size="sm" className="bg-brand-500 hover:bg-brand-400">
                                <Target className="h-4 w-4" />
                                Review opportunities
                            </Button>
                            <Button
                                size="sm"
                                variant="outline"
                                className="border-white/20 bg-white/5 text-white hover:bg-white/10 hover:text-white"
                            >
                                <Gauge className="h-4 w-4" />
                                View Scrap Index
                            </Button>
                        </div>
                    </div>

                    <div className="relative flex items-center gap-4 rounded-xl border border-white/10 bg-white/5 p-4 backdrop-blur-sm">
                        <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-brand-500/20 text-brand-300">
                            <CalendarClock className="h-5 w-5" />
                        </span>
                        <div className="leading-tight">
                            <p className="text-xs font-medium text-steel-400">
                                Your last sign-in was
                            </p>
                            <p className="mt-0.5 text-sm font-semibold text-white">
                                September 30, 2026 at 10:21:29 AM
                            </p>
                        </div>
                    </div>
                </div>
            </section>

            {/* Live pricing ticker */}
            <div className="mt-6 flex items-center gap-4 rounded-xl border border-border bg-card px-4 py-3 shadow-soft">
                <span className="flex shrink-0 items-center gap-2 border-r border-border pr-4 text-xs font-bold uppercase tracking-wide text-brand-600">
                    <span className="h-2 w-2 animate-pulse rounded-full bg-brand-500" />
                    Live Pricing
                </span>
                <PriceTicker items={TICKER} />
            </div>

            {/* Stats */}
            <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {STATS.map((stat, i) => (
                    <StatCard key={stat.title} stat={stat} loading={loading} index={i} />
                ))}
            </div>

            <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-3">
                {/* Market pulse */}
                <Card className="xl:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">
                                Scrap Index Pulse
                            </CardTitle>
                            <p className="mt-1 text-sm text-muted-foreground">
                                Composite pricing across tracked commodities
                            </p>
                        </div>
                        <Badge variant="success">
                            <ArrowUpRight className="h-3.5 w-3.5" />
                            +2.4%
                        </Badge>
                    </CardHeader>
                    <CardContent>
                        {loading ? (
                            <Skeleton className="h-48 w-full" />
                        ) : (
                            <MarketChart />
                        )}
                    </CardContent>
                </Card>

                {/* Onboarding progress */}
                <Card>
                    <CardHeader>
                        <CardTitle className="text-base font-semibold">Get set up</CardTitle>
                    </CardHeader>
                    <CardContent className="flex flex-col items-center gap-5">
                        {loading ? (
                            <Skeleton className="h-32 w-32 rounded-full" />
                        ) : (
                            <ProgressRing value={68} sublabel="Complete" />
                        )}
                        <ul className="w-full space-y-2.5">
                            {[
                                { label: 'Verify your organization', done: true },
                                { label: 'Invite your team', done: true },
                                { label: 'Connect Quickbase tables', done: false },
                                { label: 'Set pricing alerts', done: false },
                            ].map((step) => (
                                <li key={step.label} className="flex items-center gap-2.5 text-sm">
                                    <span
                                        className={cn(
                                            'flex h-5 w-5 shrink-0 items-center justify-center rounded-full',
                                            step.done
                                                ? 'bg-brand-500 text-white'
                                                : 'border border-border bg-muted text-transparent'
                                        )}
                                    >
                                        <CheckCircle2 className="h-3.5 w-3.5" />
                                    </span>
                                    <span
                                        className={cn(
                                            step.done
                                                ? 'text-muted-foreground line-through'
                                                : 'font-medium text-ink-800'
                                        )}
                                    >
                                        {step.label}
                                    </span>
                                </li>
                            ))}
                        </ul>
                    </CardContent>
                </Card>
            </div>

            <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-3">
                {/* Opportunities table */}
                <Card className="xl:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <CardTitle className="text-base font-semibold">
                            Qualified Opportunities
                        </CardTitle>
                        <Button variant="ghost" size="sm" className="text-brand-600">
                            View all
                        </Button>
                    </CardHeader>
                    <CardContent className="px-0">
                        {loading ? (
                            <div className="space-y-3 px-6">
                                {[...Array(4)].map((_, i) => (
                                    <Skeleton key={i} className="h-12 w-full" />
                                ))}
                            </div>
                        ) : (
                            <div className="overflow-x-auto">
                                <table className="w-full min-w-[620px] text-sm">
                                    <thead>
                                        <tr className="border-y border-border bg-muted/50 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                                            <th className="px-6 py-3">Opportunity</th>
                                            <th className="px-6 py-3">Material</th>
                                            <th className="px-6 py-3">Seller</th>
                                            <th className="px-6 py-3">Status</th>
                                            <th className="px-6 py-3 text-right">Est. Value</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-border">
                                        {OPPORTUNITIES.map((opp) => (
                                            <tr
                                                key={opp.id}
                                                className="transition-colors hover:bg-muted/40"
                                            >
                                                <td className="px-6 py-3.5 font-semibold text-ink-900">
                                                    {opp.id}
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {opp.material}
                                                </td>
                                                <td className="px-6 py-3.5">
                                                    <span className="block text-ink-800">
                                                        {opp.seller}
                                                    </span>
                                                    <span className="mt-0.5 inline-flex items-center gap-1 text-xs text-muted-foreground">
                                                        <MapPin className="h-3 w-3" />
                                                        {opp.location}
                                                    </span>
                                                </td>
                                                <td className="px-6 py-3.5">
                                                    <Badge variant={STATUS_VARIANT[opp.status]}>
                                                        {opp.status}
                                                    </Badge>
                                                </td>
                                                <td className="px-6 py-3.5 text-right font-semibold text-ink-900">
                                                    {opp.value}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </CardContent>
                </Card>

                {/* Activity feed */}
                <Card>
                    <CardHeader>
                        <CardTitle className="text-base font-semibold">Recent Activity</CardTitle>
                    </CardHeader>
                    <CardContent>
                        {loading ? (
                            <div className="space-y-4">
                                {[...Array(4)].map((_, i) => (
                                    <div key={i} className="flex items-start gap-3">
                                        <Skeleton className="h-9 w-9 rounded-lg" />
                                        <div className="flex-1 space-y-2">
                                            <Skeleton className="h-3.5 w-full" />
                                            <Skeleton className="h-3 w-24" />
                                        </div>
                                    </div>
                                ))}
                            </div>
                        ) : (
                            <ol className="relative space-y-5">
                                {ACTIVITY.map((item, i) => {
                                    const Icon = item.icon
                                    return (
                                        <li key={i} className="flex items-start gap-3">
                                            <span
                                                className={cn(
                                                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
                                                    item.tone
                                                )}
                                            >
                                                <Icon className="h-4 w-4" />
                                            </span>
                                            <div className="min-w-0 flex-1">
                                                <p className="text-sm leading-snug text-ink-800">
                                                    {item.text}
                                                </p>
                                                <p className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                                                    <span className="font-medium">{item.user}</span>
                                                    <span className="inline-flex items-center gap-1">
                                                        <Clock className="h-3 w-3" />
                                                        {item.time}
                                                    </span>
                                                </p>
                                            </div>
                                        </li>
                                    )
                                })}
                            </ol>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Jump back in */}
            <div className="mt-6">
                <div className="mb-3 flex items-center justify-between">
                    <h2 className="text-base font-semibold text-ink-900">Jump back in</h2>
                    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                        <Building2 className="h-3.5 w-3.5" />
                        ScrapGo workspace
                    </span>
                </div>
                <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
                    {APPS.map(({ icon: Icon, label, tone }) => (
                        <button
                            key={label}
                            className="group flex flex-col items-center gap-3 rounded-xl border border-border bg-card p-5 text-center shadow-soft transition-all hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-elevated"
                        >
                            <span
                                className={cn(
                                    'flex h-12 w-12 items-center justify-center rounded-xl transition-transform group-hover:scale-105',
                                    tone
                                )}
                            >
                                <Icon className="h-5 w-5" />
                            </span>
                            <span className="text-sm font-medium text-ink-800">{label}</span>
                        </button>
                    ))}
                </div>
            </div>
        </AppShell>
    )
}

