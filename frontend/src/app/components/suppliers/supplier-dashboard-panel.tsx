import { useState } from 'react'
import {
    Building2,
    Calendar,
    CheckCircle2,
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
import { formatCurrency, formatDate } from '../../lib/supplier-format'

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

    const [dealForm, setDealForm] = useState({
        materialType: '',
        estimatedQuantity: '',
        packaging: '',
        readyToShip: '',
        askingPrice: '',
        additionalNotes: '',
    })
    const [dealSubmitting, setDealSubmitting] = useState(false)
    const [dealSubmitSuccess, setDealSubmitSuccess] = useState(false)

    const [pickupForm, setPickupForm] = useState({
        materialForPickup: '',
        truckTypeNeeded: '',
        preferredPickupDate: '',
        preferredTime: '',
        estimatedLoadWeight: '',
        numberOfTrucks: '',
        pickupAddress: '',
        specialInstructions: '',
    })
    const [pickupSubmitting, setPickupSubmitting] = useState(false)
    const [pickupSubmitSuccess, setPickupSubmitSuccess] = useState(false)

    const onSubmitDeal = (e: React.FormEvent) => {
        e.preventDefault()
        setDealSubmitting(true)
        // TODO: Submit to backend when deal submission endpoint exists
        // Currently no backend endpoint for deal submission
        setTimeout(() => {
            setDealSubmitting(false)
            setDealSubmitSuccess(true)
        }, 1500)
    }

    const onSubmitPickup = (e: React.FormEvent) => {
        e.preventDefault()
        setPickupSubmitting(true)
        // TODO: Submit to backend when pickup request endpoint exists
        // Currently no backend endpoint for pickup requests
        setTimeout(() => {
            setPickupSubmitting(false)
            setPickupSubmitSuccess(true)
        }, 1500)
    }

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

            {/* Dashboard tab content */}
            {activeTab === 'dashboard' && (
                <>
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
                </>
            )}

            {/* Payments tab content */}
            {activeTab === 'payments' && (
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <StatCard
                        title="Total Paid (YTD)"
                        value={formatCurrency(supplier.deliveredLast90Days ?? 0)}
                        subtitle={`${supplier.totalActivities ?? 0} loads · YTD`}
                        icon={DollarSign}
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
                        title="Payment Terms"
                        value={supplier.paymentTerms ?? '—'}
                        subtitle="Paid Fridays"
                        icon={Building2}
                        tone="bg-brand-500/10 text-brand-500 border-brand-500/20"
                    />
                </div>
            )}

            {/* Payment History table */}
            {activeTab === 'payments' && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Payment History</CardTitle>
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
                                        <th className="px-6 py-3">Delivery Date</th>
                                        <th className="px-6 py-3">Status</th>
                                        <th className="px-6 py-3 text-right">Amount Paid</th>
                                        <th className="px-6 py-3 text-right">Payment Date</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-border">
                                    {recentLoads.length === 0 ? (
                                        <tr>
                                            <td colSpan={8} className="px-6 py-8 text-center text-muted-foreground">
                                                No payment history found.
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
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {formatDate(load.shipDate) ?? '—'}
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {formatDate(load.deliveryDate) ?? '—'}
                                                </td>
                                                <td className="px-6 py-3.5">
                                                    <Badge variant="success">
                                                        {load.status ?? '—'}
                                                    </Badge>
                                                </td>
                                                <td className="px-6 py-3.5 text-right font-semibold text-ink-900">
                                                    {formatCurrency(load.weight ?? 0)}
                                                </td>
                                                <td className="px-6 py-3.5 text-right text-xs text-muted-foreground">
                                                    —
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </CardContent>
                </Card>
            )}

            {/* Load History tab content */}
            {activeTab === 'load-history' && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Load History</CardTitle>
                            <p className="mt-1 text-xs text-muted-foreground">
                                All loads delivered by {supplier.account || 'this supplier'}.
                            </p>
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
                                        <th className="px-6 py-3">Delivery Date</th>
                                        <th className="px-6 py-3">Status</th>
                                        <th className="px-6 py-3 text-right">Consumer</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-border">
                                    {recentLoads.length === 0 ? (
                                        <tr>
                                            <td colSpan={7} className="px-6 py-8 text-center text-muted-foreground">
                                                No load history found.
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
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {formatDate(load.shipDate) ?? '—'}
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">
                                                    {formatDate(load.deliveryDate) ?? '—'}
                                                </td>
                                                <td className="px-6 py-3.5">
                                                    <Badge variant="success">
                                                        {load.status ?? '—'}
                                                    </Badge>
                                                </td>
                                                <td className="px-6 py-3.5 text-ink-700">
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
            )}

            {/* Submit a Deal tab content */}
            {activeTab === 'submit-deal' && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Submit a Deal Opportunity</CardTitle>
                            <p className="mt-1 text-xs text-muted-foreground">
                                Have material ready to move? Submit the details and we'll give you a competitive offer fast.
                            </p>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <form onSubmit={onSubmitDeal} className="space-y-4">
                            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Material Type
                                    </label>
                                    <select
                                        value={dealForm.materialType}
                                        onChange={(e) => setDealForm((prev) => ({ ...prev, materialType: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    >
                                        <option value="">Select material type</option>
                                        <option value="Crushed Cars w/o Motors">Crushed Cars w/o Motors</option>
                                        <option value="HMS 80:20">HMS 80:20</option>
                                        <option value="Aluminum Wheels">Aluminum Wheels</option>
                                        <option value="Copper #2">Copper #2</option>
                                        <option value="Yellow Brass">Yellow Brass</option>
                                        <option value="Shredded">Shredded</option>
                                    </select>
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Estimated Quantity
                                    </label>
                                    <input
                                        type="text"
                                        value={dealForm.estimatedQuantity}
                                        onChange={(e) => setDealForm((prev) => ({ ...prev, estimatedQuantity: e.target.value }))}
                                        placeholder="e.g. 20 NT or 40,000 lbs"
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Packaging
                                    </label>
                                    <select
                                        value={dealForm.packaging}
                                        onChange={(e) => setDealForm((prev) => ({ ...prev, packaging: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    >
                                        <option value="">Select packaging</option>
                                        <option value="Crushed / Flattened">Crushed / Flattened</option>
                                        <option value="Loose">Loose</option>
                                        <option value="Baled">Baled</option>
                                        <option value="Gaylord Boxes">Gaylord Boxes</option>
                                    </select>
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Ready to Ship By
                                    </label>
                                    <input
                                        type="date"
                                        value={dealForm.readyToShip}
                                        onChange={(e) => setDealForm((prev) => ({ ...prev, readyToShip: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    />
                                </div>
                            </div>
                            <div className="space-y-2">
                                <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                    Your Asking Price (Optional)
                                </label>
                                <input
                                    type="text"
                                    value={dealForm.askingPrice}
                                    onChange={(e) => setDealForm((prev) => ({ ...prev, askingPrice: e.target.value }))}
                                    placeholder="e.g. $195/NT"
                                    className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                />
                            </div>
                            <div className="space-y-2">
                                <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                    Additional Notes
                                </label>
                                <textarea
                                    value={dealForm.additionalNotes}
                                    onChange={(e) => setDealForm((prev) => ({ ...prev, additionalNotes: e.target.value }))}
                                    placeholder="Condition, access details, any special handling requirements..."
                                    rows={3}
                                    className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                />
                            </div>
                            <Button
                                type="submit"
                                disabled={dealSubmitting}
                                className="w-full bg-brand-600 text-white hover:bg-brand-500 disabled:bg-brand-600/50"
                            >
                                {dealSubmitting ? 'Submitting...' : 'Submit Deal Opportunity'}
                            </Button>
                            {dealSubmitSuccess && (
                                <div className="mt-4 p-3 rounded-lg bg-emerald-50 border border-emerald-100">
                                    <div className="flex items-start gap-2">
                                        <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5" />
                                        <div>
                                            <p className="text-sm font-medium text-emerald-800">Deal opportunity submitted</p>
                                            <p className="text-xs text-emerald-600">
                                                Our team will review your submission and get back to you with a competitive offer.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </form>
                    </CardContent>
                </Card>
            )}

            {/* Request Pickup tab content */}
            {activeTab === 'request-pickup' && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Request Truck Pickup</CardTitle>
                            <p className="mt-1 text-xs text-muted-foreground">
                                Request a truck and we'll dispatch a driver to your yard. A rep will confirm availability within 24 hours.
                            </p>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <form onSubmit={onSubmitPickup} className="space-y-4">
                            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Material for Pickup
                                    </label>
                                    <select
                                        value={pickupForm.materialForPickup}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, materialForPickup: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    >
                                        <option value="">Select material</option>
                                        <option value="Crushed Cars w/o Motors">Crushed Cars w/o Motors</option>
                                        <option value="HMS 80:20">HMS 80:20</option>
                                        <option value="Aluminum Wheels">Aluminum Wheels</option>
                                        <option value="Copper #2">Copper #2</option>
                                        <option value="Yellow Brass">Yellow Brass</option>
                                        <option value="Shredded">Shredded</option>
                                    </select>
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Truck Type Needed
                                    </label>
                                    <select
                                        value={pickupForm.truckTypeNeeded}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, truckTypeNeeded: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    >
                                        <option value="">Select truck type</option>
                                        <option value="Flatbed Trailer">Flatbed Trailer</option>
                                        <option value="Van Trailer">Van Trailer</option>
                                        <option value="Dump Trailer">Dump Trailer</option>
                                        <option value="Roll-Off">Roll-Off</option>
                                    </select>
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Preferred Pickup Date
                                    </label>
                                    <input
                                        type="date"
                                        value={pickupForm.preferredPickupDate}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, preferredPickupDate: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Preferred Time
                                    </label>
                                    <select
                                        value={pickupForm.preferredTime}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, preferredTime: e.target.value }))}
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    >
                                        <option value="">Select time window</option>
                                        <option value="Morning (7am-12pm)">Morning (7am-12pm)</option>
                                        <option value="Afternoon (12pm-5pm)">Afternoon (12pm-5pm)</option>
                                        <option value="Anytime">Anytime</option>
                                    </select>
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Estimated Load Weight
                                    </label>
                                    <input
                                        type="text"
                                        value={pickupForm.estimatedLoadWeight}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, estimatedLoadWeight: e.target.value }))}
                                        placeholder="e.g. 20 NT"
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                        Number of Trucks
                                    </label>
                                    <input
                                        type="number"
                                        min="1"
                                        value={pickupForm.numberOfTrucks}
                                        onChange={(e) => setPickupForm((prev) => ({ ...prev, numberOfTrucks: e.target.value }))}
                                        placeholder="1"
                                        className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                    />
                                </div>
                            </div>
                            <div className="space-y-2">
                                <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                    Pickup Address
                                </label>
                                <input
                                    type="text"
                                    value={pickupForm.pickupAddress}
                                    onChange={(e) => setPickupForm((prev) => ({ ...prev, pickupAddress: e.target.value }))}
                                    placeholder="Street address, city, state, ZIP"
                                    className="h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                />
                            </div>
                            <div className="space-y-2">
                                <label className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                    Special Instructions
                                </label>
                                <textarea
                                    value={pickupForm.specialInstructions}
                                    onChange={(e) => setPickupForm((prev) => ({ ...prev, specialInstructions: e.target.value }))}
                                    placeholder="Gate codes, loading dock access, contact on site..."
                                    rows={3}
                                    className="w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-ink-900 focus:ring-2 focus:ring-brand-500"
                                />
                            </div>
                            <Button
                                type="submit"
                                disabled={pickupSubmitting}
                                className="w-full bg-brand-600 text-white hover:bg-brand-500 disabled:bg-brand-600/50"
                            >
                                {pickupSubmitting ? 'Submitting...' : 'Submit Pickup Request'}
                            </Button>
                            {pickupSubmitSuccess && (
                                <div className="mt-4 p-3 rounded-lg bg-emerald-50 border border-emerald-100">
                                    <div className="flex items-start gap-2">
                                        <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5" />
                                        <div>
                                            <p className="text-sm font-medium text-emerald-800">Pickup request submitted</p>
                                            <p className="text-xs text-emerald-600">
                                                A rep will confirm availability within 24 hours.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </form>
                    </CardContent>
                </Card>
            )}

            {/* Messages tab content */}
            {activeTab === 'messages' && (
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0">
                        <div>
                            <CardTitle className="text-base font-semibold">Messages</CardTitle>
                            <p className="mt-1 text-xs text-muted-foreground">
                                Coming soon — direct messaging with your ScrapGo team.
                            </p>
                        </div>
                    </CardHeader>
                    <CardContent className="flex flex-col h-[500px] items-center justify-center">
                        <div className="text-center">
                            <MessageSquare className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                            <h3 className="text-lg font-semibold text-ink-900">Messages coming soon</h3>
                            <p className="text-sm text-muted-foreground mt-1">
                                Direct messaging with your ScrapGo team will be available in a future update.
                            </p>
                        </div>
                    </CardContent>
                </Card>
            )}
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
