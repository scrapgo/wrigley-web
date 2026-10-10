import { AlertCircle, Phone, RefreshCw } from 'lucide-react'

import { Button } from '../ui/button'
import { Badge } from '../ui/badge'
import { Checkbox } from '../ui/checkbox'
import { Skeleton } from '../ui/skeleton'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { FreshnessBadge } from './freshness-badge'
import { RichText } from './rich-text'
import { adminErrorMessage } from '../../lib/admin-errors'
import { cn } from '../../lib/utils'
import { htmlToText, formatCurrency, formatDate, formatNumber } from '../../lib/supplier-format'
import {
    catalogLabel,
    useSupplierCallProspectStatus,
    useSupplierCatalogs,
    useSupplierTargetPricing,
    useSupplierYardCapabilities,
} from '../../hooks/useSupplierQueries'
import type { DataFreshness, SupplierDetail, SupplierYardCapabilities } from '../../lib/supplier-api'

/* -------------------------------------------------------------------------- */
/*  Shared presentational helpers                                             */
/* -------------------------------------------------------------------------- */

export function DetailField({
    label,
    value,
    richText = false,
}: {
    label: string
    value: string | null | undefined
    richText?: boolean
}) {
    return (
        <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {label}
            </dt>
            <dd className="mt-1 text-sm text-ink-800">
                <RichText value={value} richText={richText} />
            </dd>
        </div>
    )
}

function DetailBlock({
    label,
    value,
    richText = false,
}: {
    label: string
    value: string | null | undefined
    richText?: boolean
}) {
    return (
        <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {label}
            </dt>
            <dd className="mt-1 whitespace-pre-wrap text-sm text-ink-800">
                <RichText value={value} richText={richText} />
            </dd>
        </div>
    )
}

/**
 * A card whose header shows a title and the data's freshness, and whose body
 * handles loading and error states for one supplier section.
 */
function DataSection({
    title,
    freshness,
    isLoading,
    isError,
    error,
    onRetry,
    children,
}: {
    title: string
    freshness?: DataFreshness
    isLoading: boolean
    isError: boolean
    error: unknown
    onRetry?: () => void
    children: React.ReactNode
}) {
    return (
        <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0">
                <CardTitle className="text-base font-semibold">{title}</CardTitle>
                {!isLoading && freshness && <FreshnessBadge freshness={freshness} />}
            </CardHeader>
            <CardContent>
                {isLoading ? (
                    <SectionSkeleton />
                ) : isError ? (
                    <SectionError error={error} onRetry={onRetry} />
                ) : (
                    children
                )}
            </CardContent>
        </Card>
    )
}

function SectionSkeleton() {
    return (
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
            {[0, 1, 2, 3, 4, 5].map((i) => (
                <div key={i} className="space-y-2">
                    <Skeleton className="h-3 w-24" />
                    <Skeleton className="h-4 w-40" />
                </div>
            ))}
        </div>
    )
}

function SectionError({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
    return (
        <div className="flex flex-col items-start gap-3 text-sm text-muted-foreground">
            <span className="inline-flex items-center gap-2 text-amber-700">
                <AlertCircle className="h-4 w-4 shrink-0" />
                {adminErrorMessage(error)}
            </span>
            {onRetry && (
                <Button variant="outline" size="sm" onClick={onRetry}>
                    <RefreshCw className="h-4 w-4" />
                    Try again
                </Button>
            )}
        </div>
    )
}

/* -------------------------------------------------------------------------- */
/*  Primary contact                                                           */
/* -------------------------------------------------------------------------- */

export function SupplierPrimaryContactCard({ supplier }: { supplier: SupplierDetail }) {
    const name =
        htmlToText(supplier.mainContactNames[0]) ??
        htmlToText(supplier.leadAssignedTo?.name) ??
        'No contact on file'
    const leadName = htmlToText(supplier.leadAssignedTo?.name)

    return (
        <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0">
                <CardTitle className="text-base font-semibold">Primary Contact</CardTitle>
            </CardHeader>
            <CardContent className="space-y-5">
                <div className="flex items-center gap-3">
                    <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-brand-500 text-sm font-bold uppercase text-white">
                        {initials(name)}
                    </span>
                    <div className="min-w-0">
                        <p className="truncate font-semibold text-ink-900">{name}</p>
                        {leadName && supplier.mainContactNames.length > 0 && (
                            <p className="truncate text-xs text-muted-foreground">
                                Lead: {leadName}
                            </p>
                        )}
                    </div>
                </div>
                <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
                    <DetailField label="Phone" value={supplier.mainContactPhone} />
                    <DetailField label="Email" value={supplier.mainEmail} />
                </dl>
                {(supplier.mainContactPhone || supplier.mainEmail) && (
                    <div className="flex flex-wrap gap-2">
                        {supplier.mainContactPhone && (
                            <Button variant="outline" size="sm" asChild>
                                <a href={`tel:${htmlToText(supplier.mainContactPhone)}`}>
                                    <Phone className="h-4 w-4" />
                                    Call Now
                                </a>
                            </Button>
                        )}
                        {supplier.mainEmail && (
                            <Button variant="outline" size="sm" asChild>
                                <a href={`mailto:${htmlToText(supplier.mainEmail)}`}>Send Email</a>
                            </Button>
                        )}
                    </div>
                )}
            </CardContent>
        </Card>
    )
}

function initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean)
    if (parts.length === 0) return '?'
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase()
}

export function SupplierCallProspectStatusCard({ recordId }: { recordId: number }) {
    const { data, isLoading, isError, error, refetch } = useSupplierCallProspectStatus(recordId)
    const { data: catalogs } = useSupplierCatalogs()
    const status = data?.callProspectStatus

    return (
        <DataSection
            title="Call & Prospect Status"
            freshness={data?.freshness}
            isLoading={isLoading}
            isError={isError}
            error={error}
            onRetry={() => void refetch()}
        >
            {status && (
                <div className="space-y-5">
                    {status.contactWithDecisionMakerMade && (
                        <div className="flex items-center gap-2 rounded-lg border border-brand-200 bg-brand-50/70 px-3 py-2.5 text-sm font-medium text-brand-700">
                            <span aria-hidden>✓</span>
                            {/* Field 197 "Text Connected" — Quickbase rich-text (allowHTML). */}
                            <RichText
                                value={status.contactWithDecisionMakerMade}
                                richText
                                empty=""
                            />
                        </div>
                    )}
                    <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
                        {/* Field 192 "Color Status" — Quickbase rich-text (allowHTML). */}
                        <DetailField label="Prospect Status" value={status.prospectStatus} richText />
                        <DetailField
                            label="Last Call Result"
                            value={catalogLabel(catalogs, 'lastCallResults', status.lastCallResult)}
                        />
                        <DetailField
                            label="Supplier Objections"
                            value={catalogLabel(catalogs, 'supplierObjections', status.supplierObjection)}
                        />
                        <DetailField label="Call Back Date" value={formatDate(status.callBackDate)} />
                    </dl>
                    <DetailBlock label="Objection Explained" value={status.objectionExplained} />
                    <DetailBlock label="Call Notes" value={status.callNotes} />
                </div>
            )}
        </DataSection>
    )
}

/* -------------------------------------------------------------------------- */
/*  Yard capabilities                                                         */
/* -------------------------------------------------------------------------- */

const YARD_FIELDS: { key: Exclude<keyof SupplierYardCapabilities, 'recordId'>; label: string }[] = [
    { key: 'crusherOnSite', label: 'Crusher on Site' },
    { key: 'balerOnSite', label: 'Baler on Site' },
    { key: 'loggerOnSite', label: 'Logger on Site' },
    { key: 'hasScale', label: 'Has Scale' },
    { key: 'loadFlatbeds', label: 'Load Flatbeds' },
    { key: 'loadDumps', label: 'Load Dumps' },
    { key: 'loadVanTrailers', label: 'Load Van Trailers' },
    { key: 'hasLoadWrap', label: 'Has Load Wrap' },
    { key: 'mobileCrusher', label: 'Mobile Crusher' },
    { key: 'usesOwnTrucks', label: 'Use Own Trucks' },
    { key: 'canExport', label: 'Can Export' },
    { key: 'railAccess', label: 'Rail Access' },
    { key: 'hasGaylordBoxes', label: 'Has Gaylord Boxes' },
]

export function SupplierYardCapabilitiesCard({ recordId }: { recordId: number }) {
    const { data, isLoading, isError, error, refetch } = useSupplierYardCapabilities(recordId)
    const capabilities = data?.yardCapabilities

    return (
        <DataSection
            title="Yard Capabilities"
            freshness={data?.freshness}
            isLoading={isLoading}
            isError={isError}
            error={error}
            onRetry={() => void refetch()}
        >
            {capabilities && (
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                    {YARD_FIELDS.map(({ key, label }) => (
                        <YardCapability key={key} label={label} value={capabilities[key]} />
                    ))}
                </div>
            )}
        </DataSection>
    )
}

function YardCapability({ label, value }: { label: string; value: boolean | null }) {
    const checked = value === true
    return (
        <div
            className={cn(
                'flex items-center gap-3 rounded-lg border px-3 py-2',
                checked ? 'border-brand-200 bg-brand-50/60' : 'border-border'
            )}
        >
            <Checkbox
                checked={checked}
                tabIndex={-1}
                aria-readonly
                className="pointer-events-none"
            />
            <span
                className={cn(
                    'text-sm',
                    checked ? 'font-medium text-ink-900' : 'text-muted-foreground'
                )}
            >
                {label}
            </span>
        </div>
    )
}

/* -------------------------------------------------------------------------- */
/*  Target Pricing — Progress Rail                                            */
/* -------------------------------------------------------------------------- */

export function SupplierTargetPricingCard({ recordId }: { recordId: number }) {
    const { data, isLoading, isError, error, refetch } = useSupplierTargetPricing(recordId)
    const pricing = data?.targetPricingProgressRail

    return (
        <DataSection
            title="Target Pricing — Progress Rail"
            freshness={data?.freshness}
            isLoading={isLoading}
            isError={isError}
            error={error}
            onRetry={() => void refetch()}
        >
            {pricing && (
                <div className="space-y-6">
                    <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                        <DetailField label="Target Material" value={pricing.targetMaterial} />
                        <DetailField label="Target UOM" value={pricing.targetUom} />
                        <DetailField label="Target PO Number" value={pricing.targetPoNumber} />
                        <DetailField label="Trucks / Week" value={formatNumber(pricing.trucksPerWeek)} />
                    </dl>
                    <dl className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                        <DetailField label="Target Offer" value={formatCurrency(pricing.targetOffer)} />
                        <DetailField
                            label="Target Break Even"
                            value={formatCurrency(pricing.targetBreakEven)}
                        />
                        <DetailField
                            label="Target Consumer Price"
                            value={formatCurrency(pricing.targetConsumerPrice)}
                        />
                        <DetailField
                            label="Target FR / UOM"
                            value={formatNumber(pricing.targetFreightPerUom)}
                        />
                        <DetailField
                            label="Target Freight Cost"
                            value={formatCurrency(pricing.targetFreightCost)}
                        />
                        <DetailField
                            label="Price Change from Prior"
                            value={formatCurrency(pricing.priceChangeFromPrior)}
                        />
                    </dl>
                    <div>
                        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                            Prices by unit
                        </p>
                        <dl className="mt-3 grid grid-cols-2 gap-x-8 gap-y-5 lg:grid-cols-4">
                            <DetailField
                                label="Net Tons"
                                value={formatCurrency(pricing.priceInNetTons)}
                            />
                            <DetailField label="LBS" value={formatCurrency(pricing.priceInLbs)} />
                            <DetailField label="CWT" value={formatCurrency(pricing.priceInCwt)} />
                            <DetailField
                                label="Gross Tons"
                                value={formatCurrency(pricing.priceInGrossTons)}
                            />
                        </dl>
                    </div>
                </div>
            )}
        </DataSection>
    )
}

/* -------------------------------------------------------------------------- */
/*  Header status badges                                                      */
/* -------------------------------------------------------------------------- */

/** Prospect status and last call result badges for the page header (data-driven, self-fetching). */
export function SupplierStatusBadges({ recordId }: { recordId: number }) {
    const { data } = useSupplierCallProspectStatus(recordId)
    const { data: catalogs } = useSupplierCatalogs()
    const status = data?.callProspectStatus
    if (!status) return null

    const prospectStatus = htmlToText(status.prospectStatus)
    const lastCall = catalogLabel(catalogs, 'lastCallResults', status.lastCallResult)
    if (!prospectStatus && !lastCall) return null

    return (
        <>
            {prospectStatus && <Badge variant="default">{prospectStatus}</Badge>}
            {lastCall && <Badge variant="neutral">{lastCall}</Badge>}
        </>
    )
}
