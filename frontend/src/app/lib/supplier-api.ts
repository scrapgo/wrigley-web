// Typed wrappers around the ScrapGo.Core.Api supplier endpoints.
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useSupplierQueries` for hooks.
//
// The backend Suppliers module is read-only today. These are the platform-wide
// routes (`GET /api/suppliers`), which require `Admin.Access` at platform scope
// and a Google Workspace sign-in.

import { apiClient } from './api-client'

/* -------------------------------------------------------------------------- */
/*  DTOs (mirror the backend contracts)                                       */
/* -------------------------------------------------------------------------- */

/** A supplier in a list: enough to pick one. */
export interface SupplierSummary {
    /** Quickbase Record ID# (field 3), the only unique key. Names repeat. */
    recordId: number
    /** The supplier's name (field 8), as stored. */
    account: string
}

/** A Quickbase user, e.g. who a lead is assigned to. */
export interface SupplierUser {
    id: string | null
    email: string | null
    name: string | null
}

/** One supplier's details from the Quickbase Suppliers table. Null means empty in Quickbase. */
export interface SupplierDetail {
    recordId: number
    account: string | null
    streetAddress: string | null
    city: string | null
    state: string | null
    country: string | null
    zipCode: string | null
    mainContactPhone: string | null
    mainContactNames: string[]
    paymentTerms: string | null
    mainEmail: string | null
    leadAssignedTo: SupplierUser | null
    relevantConsumerDistances: number | null
    inStockItemRecords: string | null
    totalActivities: number | null
    targetConsumerPrice: number | null
    deliveredLast90Days: number | null
    deliveredBefore90Days: number | null
    /** Quickbase field 321, a checkbox. */
    deadFreight: boolean | null
}

/** Where the data came from: `Cache`, `Quickbase` (fetched just now) or `StaleCache`. */
export interface DataFreshness {
    source: 'Cache' | 'Quickbase' | 'StaleCache' | string
    fetchedAt: string
}

export interface SupplierListResponse {
    items: SupplierSummary[]
    skip: number
    top: number
    totalRecords: number
    freshness: DataFreshness
}

export interface SupplierResponse {
    supplier: SupplierDetail
    freshness: DataFreshness
}

/* -------------------------------------------------------------------------- */
/*  Reads                                                                      */
/* -------------------------------------------------------------------------- */

/** Platform administrators: suppliers with a name, sorted by name, one page at a time. */
export function listSuppliers(
    search?: string,
    skip: number = 0,
    top: number = 100
): Promise<SupplierListResponse> {
    const params = new URLSearchParams()
    if (search) params.append('search', search)
    params.append('skip', skip.toString())
    params.append('top', top.toString())

    return apiClient.fetchWithAuth<SupplierListResponse>(`/api/suppliers?${params.toString()}`)
}

/** Platform administrators: one supplier by its Quickbase Record ID#. */
export function getSupplier(recordId: number): Promise<SupplierResponse> {
    return apiClient.fetchWithAuth<SupplierResponse>(`/api/suppliers/${recordId}`)
}
