import { useQuery } from '@tanstack/react-query'

import {
    getSupplier,
    getSupplierCallProspectStatus,
    getSupplierTargetPricingProgressRail,
    getSupplierYardCapabilities,
    listDeadFreight,
    listLastCallResults,
    listPaymentTerms,
    listSuppliers,
    listSupplierObjections,
    type DropdownOption,
} from '../lib/supplier-api'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const supplierKeys = {
    list: (search: string, skip: number, top: number) =>
        ['suppliers', 'list', search, skip, top] as const,
    detail: (recordId: number) => ['suppliers', 'detail', recordId] as const,
    callProspectStatus: (recordId: number) =>
        ['suppliers', 'detail', recordId, 'call-prospect-status'] as const,
    yardCapabilities: (recordId: number) =>
        ['suppliers', 'detail', recordId, 'yard-capabilities'] as const,
    targetPricing: (recordId: number) =>
        ['suppliers', 'detail', recordId, 'target-pricing-progress-rail'] as const,
    catalogs: ['suppliers', 'catalogs'] as const,
}

/* -------------------------------------------------------------------------- */
/*  Queries                                                                    */
/* -------------------------------------------------------------------------- */

/** Platform administrators: a page of suppliers, optionally filtered by name. */
export function useSuppliers(search: string, skip: number, top: number) {
    return useQuery({
        queryKey: supplierKeys.list(search, skip, top),
        queryFn: () => listSuppliers(search || undefined, skip, top),
        placeholderData: (previous) => previous,
    })
}

/** Platform administrators: one supplier's details. */
export function useSupplier(recordId: number) {
    return useQuery({
        queryKey: supplierKeys.detail(recordId),
        queryFn: () => getSupplier(recordId),
        enabled: recordId > 0,
    })
}

/** One supplier's call and prospect status. */
export function useSupplierCallProspectStatus(recordId: number) {
    return useQuery({
        queryKey: supplierKeys.callProspectStatus(recordId),
        queryFn: () => getSupplierCallProspectStatus(recordId),
        enabled: recordId > 0,
    })
}

/** One supplier's yard capabilities. */
export function useSupplierYardCapabilities(recordId: number) {
    return useQuery({
        queryKey: supplierKeys.yardCapabilities(recordId),
        queryFn: () => getSupplierYardCapabilities(recordId),
        enabled: recordId > 0,
    })
}

/** One supplier's Target Pricing — Progress Rail. */
export function useSupplierTargetPricing(recordId: number) {
    return useQuery({
        queryKey: supplierKeys.targetPricing(recordId),
        queryFn: () => getSupplierTargetPricingProgressRail(recordId),
        enabled: recordId > 0,
    })
}

/** The static dropdown catalogs, as `value -> label` maps. Static, so cached for the session. */
export interface SupplierCatalogs {
    paymentTerms: Record<string, string>
    deadFreight: Record<string, string>
    lastCallResults: Record<string, string>
    supplierObjections: Record<string, string>
}

function toLabelMap(options: DropdownOption[]): Record<string, string> {
    return Object.fromEntries(options.map((option) => [option.value, option.label]))
}

export function useSupplierCatalogs() {
    return useQuery({
        queryKey: supplierKeys.catalogs,
        staleTime: Infinity,
        queryFn: async (): Promise<SupplierCatalogs> => {
            const [paymentTerms, deadFreight, lastCallResults, supplierObjections] =
                await Promise.all([
                    listPaymentTerms(),
                    listDeadFreight(),
                    listLastCallResults(),
                    listSupplierObjections(),
                ])
            return {
                paymentTerms: toLabelMap(paymentTerms),
                deadFreight: toLabelMap(deadFreight),
                lastCallResults: toLabelMap(lastCallResults),
                supplierObjections: toLabelMap(supplierObjections),
            }
        },
    })
}

/** The label for a dropdown `value`, falling back to the raw value (or `—`). */
export function catalogLabel(
    catalog: SupplierCatalogs | undefined,
    key: keyof SupplierCatalogs,
    value: string | null | undefined
): string | null {
    if (value == null) return null
    return catalog?.[key]?.[value] ?? value
}
