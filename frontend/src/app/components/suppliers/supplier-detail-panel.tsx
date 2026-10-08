import { Link } from '@tanstack/react-router'
import {
    ArrowLeft,
    Building2,
    ClipboardList,
    DollarSign,
    FileText,
    MapPin,
    Phone,
    Ruler,
    Truck,
    UserRound,
    Users,
    type LucideIcon,
} from 'lucide-react'

import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Skeleton } from '../ui/skeleton'
import { EmptyState } from '../ui/empty-state'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { useSupplier } from '../../hooks/useSupplierQueries'
import { adminErrorMessage } from '../../lib/admin-errors'
import { FreshnessBadge } from './freshness-badge'

export function SupplierDetailPanel({ recordId }: { recordId: number }) {
    const { data, isLoading, isError, error } = useSupplier(recordId)

    if (isLoading) {
        return (
            <div className="space-y-4">
                <Skeleton className="h-8 w-64" />
                <Skeleton className="h-4 w-96" />
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    {[0, 1, 2, 3].map((i) => (
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
    const location = [supplier.city, supplier.state].filter(Boolean).join(', ')
    const address = [supplier.streetAddress, location, supplier.zipCode].filter(Boolean).join(' · ')

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
                                {supplier.account || 'Unnamed supplier'}
                            </h2>
                            <Badge variant="neutral">Record #{supplier.recordId}</Badge>
                            <FreshnessBadge freshness={freshness} />
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
                                    {supplier.mainContactPhone}
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

            {/* Stats */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <StatTile
                    icon={ClipboardList}
                    label="Total Activities"
                    value={formatNumber(supplier.totalActivities)}
                />
                <StatTile
                    icon={Truck}
                    label="Delivered (90 days)"
                    value={formatNumber(supplier.deliveredLast90Days)}
                />
                <StatTile
                    icon={Users}
                    label="In-Stock Items"
                    value={supplier.inStockItemRecords || '—'}
                />
                <StatTile
                    icon={DollarSign}
                    label="Target Consumer Price"
                    value={formatCurrency(supplier.targetConsumerPrice)}
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
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base font-semibold">Supplier Details</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                <DetailField label="Supplier" value={supplier.account} />
                                <DetailField label="Record ID" value={String(supplier.recordId)} />
                                <DetailField label="Payment Terms" value={supplier.paymentTerms} />
                                <DetailField label="Street Address" value={supplier.streetAddress} />
                                <DetailField label="City" value={supplier.city} />
                                <DetailField label="State" value={supplier.state} />
                                <DetailField label="Zip Code" value={supplier.zipCode} />
                                <DetailField label="Country" value={supplier.country} />
                                <DetailField label="Main Contact Phone" value={supplier.mainContactPhone} />
                                <DetailField label="Main Email" value={supplier.mainEmail} />
                                <DetailField
                                    label="Dead Freight"
                                    value={supplier.deadFreight == null ? null : supplier.deadFreight ? 'Exempt' : 'Not exempt'}
                                />
                                <DetailField
                                    label="Lead Assigned To"
                                    value={supplier.leadAssignedTo?.name ?? supplier.leadAssignedTo?.email}
                                />
                            </dl>
                        </CardContent>
                    </Card>
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
                                    />
                                    <DetailField label="Payment Terms" value={supplier.paymentTerms} />
                                    <DetailField
                                        label="Dead Freight"
                                        value={supplier.deadFreight == null ? null : supplier.deadFreight ? 'Exempt' : 'Not exempt'}
                                    />
                                </dl>
                            </CardContent>
                        </Card>
                        <PlaceholderTab
                            icon={DollarSign}
                            title="Material pricing not available yet"
                            description="Per-material pricing and grades aren't returned by the supplier API yet."
                        />
                    </div>
                </TabsContent>

                <TabsContent value="calls">
                    <div className="space-y-6">
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
                                </dl>
                            </CardContent>
                        </Card>
                        <PlaceholderTab
                            icon={Phone}
                            title="Call history not available yet"
                            description="Individual call and prospect records aren't returned by the supplier API yet."
                        />
                    </div>
                </TabsContent>

                <TabsContent value="contacts">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base font-semibold">Contacts</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-6">
                            <div>
                                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                                    Main Contact Names
                                </p>
                                {supplier.mainContactNames.length > 0 ? (
                                    <ul className="mt-2 flex flex-wrap gap-2">
                                        {supplier.mainContactNames.map((name) => (
                                            <li key={name}>
                                                <Badge variant="default">{name}</Badge>
                                            </li>
                                        ))}
                                    </ul>
                                ) : (
                                    <p className="mt-2 text-sm text-muted-foreground">—</p>
                                )}
                            </div>
                            <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                                <DetailField label="Main Email" value={supplier.mainEmail} />
                                <DetailField label="Main Contact Phone" value={supplier.mainContactPhone} />
                                <DetailField
                                    label="Lead Assigned To"
                                    value={supplier.leadAssignedTo?.name ?? supplier.leadAssignedTo?.email}
                                />
                            </dl>
                        </CardContent>
                    </Card>
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
                    <PlaceholderTab
                        icon={FileText}
                        title="Documents &amp; other details not available yet"
                        description="Documents, notes, and other attachments aren't returned by the supplier API yet."
                    />
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
}: {
    icon: LucideIcon
    label: string
    value: string
}) {
    return (
        <Card>
            <CardContent className="flex items-center gap-4 p-5">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
                    <Icon className="h-5 w-5" />
                </span>
                <div className="min-w-0">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        {label}
                    </p>
                    <p className="mt-0.5 truncate text-lg font-bold text-ink-900">{value}</p>
                </div>
            </CardContent>
        </Card>
    )
}

function DetailField({ label, value }: { label: string; value: string | null | undefined }) {
    return (
        <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {label}
            </dt>
            <dd className="mt-1 text-sm text-ink-800">{value || '—'}</dd>
        </div>
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

function formatNumber(value: number | null | undefined): string {
    return value == null ? '—' : value.toLocaleString()
}

function formatCurrency(value: number | null | undefined): string {
    return value == null
        ? '—'
        : value.toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}
