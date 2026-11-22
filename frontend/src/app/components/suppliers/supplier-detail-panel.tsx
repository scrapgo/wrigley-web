import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import {
    ArrowLeft,
    Building2,
    ClipboardList,
    DollarSign,
    MapPin,
    Phone,
    Ruler,
    Truck,
    UserRound,
    Package,
    type LucideIcon,
} from 'lucide-react'

import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Skeleton } from '../ui/skeleton'
import { EmptyState } from '../ui/empty-state'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { catalogLabel, useSupplier, useSupplierCatalogs, useSupplierDashboardStats, useSupplierLinkedDispatchRecords } from '../../hooks/useSupplierQueries'
import { adminErrorMessage } from '../../lib/admin-errors'
import type { SupplierTooltipKey } from '../../lib/supplier-tooltips'
import { InfoTip } from '../ui/info-tip'
import { FreshnessBadge } from './freshness-badge'
import {
    DetailField,
    SupplierCallNotesHistoryCard,
    SupplierCallProspectStatusCard,
    SupplierContactsCard,
    SupplierDispatchRecordsCard,
    SupplierInStockItemsCard,
    SupplierOtherProspectInformationCard,
    SupplierPrimaryContactCard,
    SupplierTargetPricingCard,
    SupplierYardCapabilitiesCard,
    SupplierStatusBadges,
} from './supplier-detail-sections'
import { formatCurrency, formatNumber, htmlToText } from '../../lib/supplier-format'

export function SupplierDetailPanel({ recordId }: { recordId: number }) {
    const { data, isLoading, isError, error } = useSupplier(recordId)
    const { data: catalogs } = useSupplierCatalogs()
    const { data: dashboardData } = useSupplierDashboardStats(recordId)
    const { data: dispatchData } = useSupplierLinkedDispatchRecords(recordId)

    const [ntTimeframe, setNtTimeframe] = useState<'all' | 'year' | 'month' | 'week'>('all')
    const [ntUom, setNtUom] = useState<'quantity' | 'tons' | 'lbs'>('quantity')

    // Aggregates for the stat tiles. Totals come from the shared dashboard-stats
    // summary where one exists (current offer); the rest are summed over the
    // supplier's full load list, never a filtered selection.
    const stats = dashboardData
    const allLoads = dispatchData?.dispatchRecords ?? []
    const deliveredLoads = allLoads.filter(
        (load) => load.status?.toLowerCase() === 'delivered'
    )
    const scopedLoads = deliveredLoads.filter((load) =>
        isWithinTimeframe(load.shipDate, ntTimeframe)
    )
    const loadsDelivered = scopedLoads.length
    const ntDelivered = scopedLoads.reduce((sum, load) => sum + (load.weight ?? 0), 0)
    const grossProfit = deliveredLoads.reduce((sum, load) => sum + (load.grossProfit ?? 0), 0)
    const currentOfferHint =
        stats?.currentOfferSource === 'lastLoadBuyPrice' ? 'Based on last load' : undefined

    if (isLoading) {
        return (
            <div className="space-y-4">
                <Skeleton className="h-8 w-64" />
                <Skeleton className="h-4 w-96" />
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    {[0, 1, 2, 3, 4].map((i) => (
                        <Skeleton key={i} className="h-24 w-full" />
                    ))}
                </div>
                <Skeleton className="h-64 w-full" />
            </div>
        )
    }

    if (isError) {
        return (
            <EmptyState
                icon={Building2}
                title="Could not load supplier"
                description={adminErrorMessage(error)}
                action={
                    <Button variant="outline" size="sm" asChild>
                        <Link to="/suppliers">
                            <ArrowLeft className="h-4 w-4" />
                            Back to suppliers
                        </Link>
                    </Button>
                }
            />
        )
    }

    if (!data) {
        return (
            <EmptyState
                icon={Building2}
                title="Supplier not found"
                description="No supplier with that record id."
                action={
                    <Button variant="outline" size="sm" asChild>
                        <Link to="/suppliers">
                            <ArrowLeft className="h-4 w-4" />
                            Back to suppliers
                        </Link>
                    </Button>
                }
            />
        )
    }

    const { supplier, freshness } = data
    const location = [supplier.city, supplier.state]
        .map((part) => htmlToText(part))
        .filter(Boolean)
        .join(', ')
    const address = [supplier.streetAddress, location, supplier.zipCode]
        .map((part) => htmlToText(part))
        .filter(Boolean)
        .join(' · ')

    return (
        <div className="space-y-6">
            <Button variant="ghost" size="sm" asChild className="-ml-2 text-muted-foreground">
                <Link to="/suppliers">
                    <ArrowLeft className="h-4 w-4" />
                    Back to suppliers
                </Link>
            </Button>

            {/* Header */}
            <Card>
                <CardContent className="flex flex-col gap-4 p-6 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <h2 className="text-xl font-bold text-ink-900">
                                {htmlToText(supplier.account) || 'Unnamed supplier'}
                            </h2>
                            <Badge variant="neutral">Record #{supplier.recordId}</Badge>
                            <FreshnessBadge freshness={freshness} />
                            <SupplierStatusBadges recordId={supplier.recordId} />
                        </div>
                        <div className="mt-3 flex flex-col gap-1.5 text-sm text-muted-foreground">
                            {address && (
                                <span className="inline-flex items-center gap-2">
                                    <MapPin className="h-4 w-4 shrink-0" />
                                    {address}
                                </span>
                            )}
                            {supplier.mainContactPhone && (
                                <span className="inline-flex items-center gap-2">
                                    <Phone className="h-4 w-4 shrink-0" />
                                    {htmlToText(supplier.mainContactPhone)}
                                </span>
                            )}
                            {supplier.leadAssignedTo?.name && (
                                <span className="inline-flex items-center gap-2">
                                    <UserRound className="h-4 w-4 shrink-0" />
                                    Assigned to {supplier.leadAssignedTo.name}
                                </span>
                            )}
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* NT Delivered timeframe / UOM selector */}
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <span className="mr-2">NT Delivered timeframe:</span>
                <select
                    value={ntTimeframe}
                    onChange={(e) => setNtTimeframe(e.target.value as 'all' | 'year' | 'month' | 'week')}
                    className="border border-border rounded px-2 py-1 text-sm bg-background"
                >
                    <option value="all">All time</option>
                    <option value="year">Current year</option>
                    <option value="month">Current month</option>
                    <option value="week">Current week</option>
                </select>
                <span className="mx-2">UOM:</span>
                <select
                    value={ntUom}
                    onChange={(e) => setNtUom(e.target.value as 'quantity' | 'tons' | 'lbs')}
                    className="border border-border rounded px-2 py-1 text-sm bg-background"
                >
                    <option value="quantity">Quantity</option>
                    <option value="tons">Net tons</option>
                    <option value="lbs">LBS</option>
                </select>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3">
                <StatTile
                    icon={Truck}
                    label="Loads Delivered"
                    tooltip="loadsLast90Days"
                    value={formatNumber(loadsDelivered)}
                    hint={ntTimeframe === 'all' ? 'All time' : ntTimeframe}
                />
                <StatTile
                    icon={Package}
                    label="NT Delivered"
                    tooltip="weight"
                    value={formatWeight(ntDelivered, ntUom)}
                    hint={`${ntTimeframe} · ${ntUom}`}
                />
                <StatTile
                    icon={ClipboardList}
                    label="Loads Scheduled for Pickup *"
                    tooltip="totalActivities"
                    value={formatNumber(supplier.totalActivities)}
                />
                <StatTile
                    icon={DollarSign}
                    label="Current Buy Price"
                    tooltip="currentOffer"
                    value={formatCurrency(stats?.currentOffer)}
                    hint={currentOfferHint}
                />
                <StatTile
                    icon={Ruler}
                    label="Gross Profit Earned '529'"
                    tooltip="grossProfit"
                    value={formatCurrency(grossProfit)}
                    hint="All delivered loads"
                />
            </div>

            {/* Tabs */}
            <Tabs defaultValue="overview">
                <TabsList className="flex-wrap">
                    <TabsTrigger value="overview">Overview</TabsTrigger>
                    <TabsTrigger value="materials">Materials &amp; Pricing</TabsTrigger>
                    <TabsTrigger value="calls">Call History</TabsTrigger>
                    <TabsTrigger value="contacts">Contacts</TabsTrigger>
                    <TabsTrigger value="distances">Consumer Distances</TabsTrigger>
                    <TabsTrigger value="documents">Documents &amp; Other</TabsTrigger>
                </TabsList>

                <TabsContent value="overview">
                    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base font-semibold">Supplier Details</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
                                    <DetailField label="Supplier" value={supplier.account} />
                                    <DetailField label="Record ID" value={String(supplier.recordId)} />
                                    <DetailField
                                        label="Payment Terms"
                                        value={catalogLabel(catalogs, 'paymentTerms', supplier.paymentTerms)}
                                        tooltip="paymentTerms"
                                    />
                                    <DetailField
                                        label="Dead Freight"
                                        value={catalogLabel(catalogs, 'deadFreight', supplier.deadFreight)}
                                        tooltip="deadFreight"
                                    />
                                    <DetailField label="Street Address" value={supplier.streetAddress} />
                                    <DetailField label="City" value={supplier.city} />
                                    <DetailField label="State" value={supplier.state} />
                                    <DetailField label="Zip Code" value={supplier.zipCode} />
                                    <DetailField label="Country" value={supplier.country} />
                                    <DetailField label="Main Contact Phone" value={supplier.mainContactPhone} />
                                    <DetailField label="Main Email" value={supplier.mainEmail} />
                                    <DetailField
                                        label="Lead Assigned To"
                                        value={supplier.leadAssignedTo?.name ?? supplier.leadAssignedTo?.email}
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                        <SupplierPrimaryContactCard supplier={supplier} />
                        <SupplierCallProspectStatusCard recordId={supplier.recordId} />
                        <SupplierYardCapabilitiesCard recordId={supplier.recordId} />
                    </div>
                </TabsContent>

                <TabsContent value="materials">
                    <div className="space-y-6">
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base font-semibold">Pricing</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                    <DetailField
                                        label="Target Consumer Price"
                                        value={formatCurrency(supplier.targetConsumerPrice)}
                                        tooltip="targetConsumerPrice"
                                    />
                                    <DetailField
                                        label="Payment Terms"
                                        value={catalogLabel(catalogs, 'paymentTerms', supplier.paymentTerms)}
                                        tooltip="paymentTerms"
                                    />
                                    <DetailField
                                        label="Dead Freight"
                                        value={catalogLabel(catalogs, 'deadFreight', supplier.deadFreight)}
                                        tooltip="deadFreight"
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                        <SupplierTargetPricingCard recordId={supplier.recordId} />
                        <SupplierInStockItemsCard recordId={supplier.recordId} />
                    </div>
                </TabsContent>

                <TabsContent value="calls">
                    <div className="space-y-6">
                        <SupplierCallProspectStatusCard recordId={supplier.recordId} />
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base font-semibold">Activity Summary</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                    <DetailField
                                        label="Total Activities"
                                        value={formatNumber(supplier.totalActivities)}
                                    />
                                    <DetailField
                                        label="Delivered (last 90 days)"
                                        value={formatNumber(supplier.deliveredLast90Days)}
                                    />
                                    <DetailField
                                        label="Delivered (before 90 days)"
                                        value={formatNumber(supplier.deliveredBefore90Days)}
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                        <SupplierCallNotesHistoryCard recordId={supplier.recordId} />
                    </div>
                </TabsContent>

                <TabsContent value="contacts">
                    <div className="space-y-6">
                        <SupplierContactsCard recordId={supplier.recordId} />
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base font-semibold">
                                    Main Contact &amp; Assignment
                                </CardTitle>
                            </CardHeader>
                            <CardContent>
                                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                    <DetailField label="Main Email" value={supplier.mainEmail} />
                                    <DetailField
                                        label="Main Contact Phone"
                                        value={supplier.mainContactPhone}
                                    />
                                    <DetailField
                                        label="Lead Assigned To"
                                        value={
                                            supplier.leadAssignedTo?.name ??
                                            supplier.leadAssignedTo?.email
                                        }
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                    </div>
                </TabsContent>

                <TabsContent value="distances">
                    <div className="space-y-6">
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base font-semibold">Consumer Distances</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                    <DetailField
                                        label="Relevant Consumer Distances"
                                        value={formatNumber(supplier.relevantConsumerDistances)}
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                        <PlaceholderTab
                            icon={Ruler}
                            title="Distance detail not available yet"
                            description="The individual consumer distance records aren't returned by the supplier API yet."
                        />
                    </div>
                </TabsContent>

                <TabsContent value="documents">
                    <div className="space-y-6">
                        <SupplierOtherProspectInformationCard recordId={supplier.recordId} />
                        <SupplierDispatchRecordsCard recordId={supplier.recordId} />
                    </div>
                </TabsContent>
            </Tabs>
        </div>
    )
}

/* -------------------------------------------------------------------------- */
/*  Small presentational helpers                                              */
/* -------------------------------------------------------------------------- */

function StatTile({
    icon: Icon,
    label,
    value,
    hint,
    tooltip,
}: {
    icon: LucideIcon
    label: string
    value: string
    hint?: string
    tooltip?: SupplierTooltipKey
}) {
    return (
        <Card>
            <CardContent className="flex items-center gap-3 p-3">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
                    <Icon className="h-4 w-4" />
                </span>
                <div className="min-w-0">
                    <div className="flex items-center gap-1">
                        <p className="text-[0.65rem] font-medium uppercase tracking-wide text-muted-foreground">
                            {label}
                        </p>
                        {tooltip && <InfoTip field={tooltip} align="start" />}
                    </div>
                    <p className="mt-0.5 truncate text-sm font-bold text-ink-900">{value}</p>
                    {hint && <p className="truncate text-[0.65rem] text-muted-foreground">{hint}</p>}
                </div>
            </CardContent>
        </Card>
    )
}

function PlaceholderTab({
    icon: Icon,
    title,
    description,
}: {
    icon: LucideIcon
    title: string
    description: string
}) {
    return (
        <EmptyState
            icon={Icon}
            title={title}
            description={description}
        />
    )
}

/** Whether a ship date falls within the selected timeframe (relative to today). */
function isWithinTimeframe(
    date: string | null,
    timeframe: 'all' | 'year' | 'month' | 'week'
): boolean {
    if (timeframe === 'all') return true
    if (!date) return false
    const then = new Date(date)
    if (isNaN(then.getTime())) return false

    const now = new Date()
    if (timeframe === 'year') return then.getFullYear() === now.getFullYear()
    if (timeframe === 'month') {
        return then.getFullYear() === now.getFullYear() && then.getMonth() === now.getMonth()
    }
    // week: within the last 7 days
    const days = (now.getTime() - then.getTime()) / (1000 * 60 * 60 * 24)
    return days >= 0 && days < 7
}

/** Format a net-tons value in the selected unit of measure. */
function formatWeight(netTons: number, uom: 'quantity' | 'tons' | 'lbs'): string {
    if (uom === 'lbs') return `${Math.round(netTons * 2000).toLocaleString()} lbs`
    return `${netTons.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${
        uom === 'tons' ? 'tons' : 'NT'
    }`
}


