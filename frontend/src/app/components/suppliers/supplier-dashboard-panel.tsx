import { useState } from 'react'
import {
    Building2,
    Calendar,
    DollarSign,
    LayoutDashboard,
    MessageSquare,
    Phone,
    Plus,
    Truck,
} from 'lucide-react'

import { Button } from '../ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { useSupplier, useSupplierLinkedDispatchRecords } from '../../hooks/useSupplierQueries'
import { formatCurrency } from '../../lib/supplier-format'

/** The supplier portal's top navigation tabs. */
const TABS = [
    { key: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { key: 'payments', label: 'Payments', icon: DollarSign },
    { key: 'load-history', label: 'Load History', icon: Truck },
    { key: 'submit-deal', label: 'Submit a Deal', icon: Plus },
    { key: 'request-pickup', label: 'Request Pickup', icon: Calendar },
    { key: 'messages', label: 'Messages', icon: MessageSquare },
] as const

export function SupplierDashboardPanel({ recordId }: { recordId: number }) {
    const [activeTab, setActiveTab] = useState<'dashboard' | 'payments' | 'load-history' | 'submit-deal' | 'request-pickup' | 'messages'>('dashboard')

    const { data: supplierData, isLoading: supplierLoading } = useSupplier(recordId)
    const { data: dispatchData, isLoading: dispatchLoading } = useSupplierLinkedDispatchRecords(recordId)

    const supplier = supplierData?.supplier
    const recentLoads = dispatchData?.dispatchRecords ?? []

    if (supplierLoading || dispatchLoading) {
        return (
            <div className="space-y-4">
                <div className="rounded-xl border border-border bg-card p-6">
                    <div className="flex items-center gap-3">
                        <div className="h-10 w-10 rounded-full bg-muted animate-pulse" />
                        <div className="flex-1 space-y-2">
                            <div className="h-4 w-3/4 bg-muted animate-pulse" />
                            <div className="h-4 w-1/2 bg-muted animate-pulse" />
                        </div>
                    </div>
                </div>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
                    {[...Array(4)].map((_, i) => (
                        <Card key={i} className="animate-pulse">
                            <CardContent className="p-5">
                                <div className="flex items-center justify-between">
                                    <div className="space-y-2">
                                        <div className="h-4 w-24 bg-muted" />
                                        <div className="h-8 w-20 bg-muted" />
                                        <div className="h-4 w-28 bg-muted" />
                                    </div>
                                    <div className="h-9 w-9 rounded-full bg-muted" />
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            </div>
        )
    }

    if (!supplier) {
        return (
            <div className="flex min-h-screen items-center justify-center bg-steel-50 px-4">
                <div className="max-w-md rounded-xl border border-border bg-card p-8 text-center shadow-card">
                    <h2 className="text-lg font-bold text-ink-900">Supplier not found</h2>
                    <p className="mt-2 text-sm text-muted-foreground">
                        No supplier with record ID {recordId}.
                    </p>
                    <Button variant="outline" size="sm" asChild className="mt-6">
                        <a href="/suppliers">Back to suppliers</a>
                    </Button>
                </div>
            </div>
        )
    }

    const location = [supplier.city, supplier.state]
        .map((part) => part)
        .filter(Boolean)
        .join(', ')
    const address = [supplier.streetAddress, location, supplier.zipCode]
        .map((part) => part)
        .filter(Boolean)
        .join(' · ')

    return (
        <div className="space-y-4">
            {/* Supplier portal header */}
            <Card className="bg-brand-600 text-white">
                <CardContent className="p-6 sm:p-8">
                    <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
                        <div className="flex flex-col gap-1">
                            <p className="text-xs font-semibold uppercase tracking-wider text-brand-100">
                                Supplier Portal
                            </p>
                            <h2 className="text-xl font-bold text-white sm:text-2xl">
                                {supplier.account || 'Unnamed supplier'}
                            </h2>
                            <p className="text-sm text-brand-100/90">
                                Branch #{supplier.recordId} — {address || 'Apopka, FL'} · Connected
                            </p>
                        </div>
                        <div className="flex flex-col items-end gap-2 sm:items-start">
                            <div className="flex items-end gap-1">
                                <span className="text-2xl font-bold text-white">
                                    {formatCurrency(supplier.targetConsumerPrice ?? 0)}
                                </span>
                                <span className="text-sm font-medium text-brand-100">/ NT</span>
                            </div>
                            <p className="text-xs text-brand-100/80 leading-none">
                                {supplier.paymentTerms ?? 'Standard terms'}
                            </p>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Navigation tabs */}
            <div className="flex gap-1 overflow-x-auto pb-2">
                {TABS.map((tab) => {
                    const Icon = tab.icon
                    const isActive = activeTab === tab.key
                    return (
                        <button
                            key={tab.key}
                            onClick={() => setActiveTab(tab.key)}
                            className={`
                                flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-colors
                                whitespace-nowrap shrink-0
                                ${isActive
                                    ? 'bg-brand-500 text-white shadow-sm'
                                    : 'text-brand-600/70 hover:bg-brand-50 hover:text-brand-600'
                                }
                            `}
                        >
                            <Icon className="h-4 w-4" />
                            {tab.label}
                        </button>
                    )
                })}
            </div>

            {/* Stat cards */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <StatCard
                    title="Total Loads (YTD)"
                    value={String(supplier.totalActivities ?? 0)}
                    subtitle="All delivered loads"
                    icon={Truck}
                    tone="bg-emerald-500/10 text-emerald-500 border-emerald-500/20"
                />
                <StatCard
                    title="Pending Payment"
                    value="—"
                    subtitle="Not yet available"
                    icon={Calendar}
                    tone="bg-muted/50 text-muted-foreground border-muted/20"
                />
                <StatCard
                    title="Loads (Last 90 Days)"
                    value={String(supplier.deliveredLast90Days ?? 0)}
                    subtitle="Recent deliveries"
                    icon={DollarSign}
                    tone="bg-muted/50 text-muted-foreground border-muted/20"
                />
                <StatCard
                    title="Current Offer"
                    value={formatCurrency(supplier.targetConsumerPrice ?? 0)}
                    subtitle={supplier.paymentTerms ?? 'Standard terms'}
                    icon={Building2}
                    tone="bg-brand-500/10 text-brand-500 border-brand-500/20"
                />
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Recent Loads */}
                <Card className="lg:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Recent Loads</CardTitle>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <div className="overflow-x-auto">
                            <table className="w-full text-sm">
                                <thead>
                                    <tr className="border-b border-border text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                                        <th className="px-6 py-3">Load #</th>
                                        <th className="px-6 py-3">Material</th>
                                        <th className="px-6 py-3">Weight</th>
                                        <th className="px-6 py-3">Ship Date</th>
                                        <th className="px-6 py-3">Status</th>
                                        <th className="px-6 py-3 text-right">Consumer</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-border">
                                    {recentLoads.length === 0 ? (
                                        <tr>
                                            <td colSpan={6} className="px-6 py-8 text-center text-muted-foreground">
                                                No recent loads found.
                                            </td>
                                        </tr>
                                    ) : (
                                        recentLoads.map((load) => (
                                            <tr key={load.recordId} className="transition-colors hover:bg-muted/40">
                                                <td className="px-6 py-3.5 font-mono text-xs text-muted-foreground">
                                                    #{load.loadNumber}
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">{load.material ?? '—'}</td>
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {load.weight != null ? `${load.weight} NT` : '—'}
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">{load.shipDate ?? '—'}</td>
                                                <td className="px-6 py-3.5">
                                                    <Badge variant="success">
                                                        {load.status ?? '—'}
                                                    </Badge>
                                                </td>
                                                <td className="px-6 py-3.5 text-right font-semibold text-ink-900">
                                                    {load.consumer ?? '—'}
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </CardContent>
                </Card>

                {/* Your ScrapGo Rep & Quick Actions */}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <CardTitle className="text-base font-semibold">Your ScrapGo Rep</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="flex items-center gap-3">
                            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-100 text-brand-600 font-bold text-sm">
                                {supplier.leadAssignedTo?.name?.charAt(0) || 'J'}
                            </div>
                            <div className="min-w-0 flex-1">
                                <p className="text-sm font-semibold text-ink-900">
                                    {supplier.leadAssignedTo?.name || 'ScrapGo Rep'}
                                </p>
                                <p className="text-xs text-muted-foreground">
                                    {supplier.leadAssignedTo?.email || supplier.mainEmail || '—'}
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center gap-2">
                            <Button variant="outline" size="sm" className="flex-1">
                                <Phone className="h-3.5 w-3.5" />
                                <span className="ml-1 text-xs">Call</span>
                            </Button>
                            <Button size="sm" className="flex-1 bg-brand-600 text-white">
                                <MessageSquare className="h-3.5 w-3.5" />
                                <span className="ml-1 text-xs">Message</span>
                            </Button>
                        </div>

                        <div className="pt-4 border-t border-border">
                            <CardTitle className="text-base font-semibold">Quick Actions</CardTitle>
                            <div className="mt-2 space-y-2">
                                <QuickActionButton icon={Plus} label="Submit a Deal Opportunity" />
                                <QuickActionButton icon={Truck} label="Request Truck Pickup" />
                                <QuickActionButton icon={Calendar} label="Check Load Weight" />
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    )
}

function StatCard({
    title,
    value,
    subtitle,
    icon: Icon,
    tone,
}: {
    title: string
    value: string
    subtitle: string
    icon: React.ComponentType<{ className?: string }>
    tone: string
}) {
    return (
        <Card className={tone}>
            <CardContent className="p-5">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-xs font-medium text-muted-foreground">{title}</p>
                        <p className="mt-1 text-2xl font-bold text-ink-900">{value}</p>
                        <p className="mt-0.5 text-xs text-muted-foreground">{subtitle}</p>
                    </div>
                    <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/50">
                        <Icon className="h-4 w-4 text-ink-900" />
                    </div>
                </div>
            </CardContent>
        </Card>
    )
}

function QuickActionButton({
    icon: Icon,
    label,
}: {
    icon: React.ComponentType<{ className?: string }>
    label: string
}) {
    return (
        <Button
            variant="outline"
            size="sm"
            className="w-full justify-start text-left text-sm"
        >
            <Icon className="h-4 w-4 mr-2" />
            {label}
        </Button>
    )
}
