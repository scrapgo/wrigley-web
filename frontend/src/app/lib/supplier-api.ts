// Typed wrappers around the ScrapGo.Core.Api supplier endpoints.
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useSupplierQueries` for hooks.
//
// The backend Suppliers module is read-only today. Every route needs
// `suppliers.view_all` (every supplier) or `suppliers.view_own` (only the
// caller's linked suppliers; anything else is 404).

import { apiClient } from './api-client'

/* -------------------------------------------------------------------------- */
/*  Quickbase field metadata (Suppliers table bqrcgnatz)                      */
/* -------------------------------------------------------------------------- */

/**
 * Suppliers-table field ids whose Quickbase `type` is `rich-text` — the fields
 * that store HTML meant to be rendered in the cell (e.g. a colored status chip
 * or a "Delete" button). Everything else is plain text/number/checkbox/user.
 *
 * Derived from the `fields` block Quickbase returns in every query response
 * (`response.fields[] = { id, label, type, properties }`). We can't read that
 * block from here yet, so the ids are pinned here; keep in sync if the table
 * changes. Rendered (sanitized) via `<RichText richText />`.
 *
 * | Id  | Quickbase label          | DTO field                      |
 * | --- | ------------------------ | ------------------------------ |
 * | 192 | Color Status             | `prospectStatus`               |
 * | 197 | Text Connected           | `contactWithDecisionMakerMade` |
 */
export const RICH_TEXT_FIELD_IDS = [192, 197] as const

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

/**
 * Dead freight (Quickbase field 321, a checkbox). Sent and accepted by name:
 * `'Exempt'` is unchecked, `'NotExempt'` is checked. Labels come from the
 * dead-freight catalog.
 */
export type DeadFreightValue = 'Exempt' | 'NotExempt'

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
    /** Quickbase field 321, a checkbox: `'Exempt'` (unchecked) or `'NotExempt'` (checked). */
    deadFreight: DeadFreightValue | null
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

/** A supplier's call and prospect status (Quickbase Suppliers table). Null means empty in Quickbase. */
export interface SupplierCallProspectStatus {
    recordId: number
    /** Field 197, "Contact with Decision Maker Has Been Made" (text). */
    contactWithDecisionMakerMade: string | null
    /** Field 192 (text). */
    prospectStatus: string | null
    /** Field 193 (dropdown). Use the last-call-results catalog for a label. */
    lastCallResult: string | null
    /** Field 236, "Supplier Objections" (dropdown). Use the supplier-objections catalog for a label. */
    supplierObjection: string | null
    /** Field 181 (date/time, UTC). */
    callBackDate: string | null
    /** Field 238 (text). */
    objectionExplained: string | null
    /** Field 97 (text). */
    callNotes: string | null
}

export interface SupplierCallProspectStatusResponse {
    callProspectStatus: SupplierCallProspectStatus
    freshness: DataFreshness
}

/** What a supplier's yard can do. Every field is a checkbox; null only if Quickbase sent no value. */
export interface SupplierYardCapabilities {
    recordId: number
    /** Field 65, "Crusher on Site?". */
    crusherOnSite: boolean | null
    /** Field 186, "Logger on Site?". */
    loggerOnSite: boolean | null
    /** Field 78, "Load Flatbeds?". */
    loadFlatbeds: boolean | null
    /** Field 182, "Load Dumps?". */
    loadDumps: boolean | null
    /** Field 225, "Mobile Crusher". */
    mobileCrusher: boolean | null
    /** Field 204, "Can Export?". */
    canExport: boolean | null
    /** Field 205, "Has Gaylord Boxes?". */
    hasGaylordBoxes: boolean | null
    /** Field 185, "Baler on Site?". */
    balerOnSite: boolean | null
    /** Field 359, "Has Scale?". */
    hasScale: boolean | null
    /** Field 183, "Load Van Trailers?". */
    loadVanTrailers: boolean | null
    /** Field 187, "Has Load Wrap?". */
    hasLoadWrap: boolean | null
    /** Field 184, "Use Own Trucks?". */
    usesOwnTrucks: boolean | null
    /** Field 230, "Rail Access". */
    railAccess: boolean | null
}

export interface SupplierYardCapabilitiesResponse {
    yardCapabilities: SupplierYardCapabilities
    freshness: DataFreshness
}

/**
 * A supplier's target pricing for Progress Rail (Quickbase "Target Pricing —
 * Progress Rail"). Currency and numeric fields are decimals; null means empty.
 */
export interface SupplierTargetPricingProgressRail {
    recordId: number
    /** Field 352 (text). */
    targetMaterial: string | null
    /** Field 342 (currency). */
    targetBreakEven: number | null
    /** Field 336 (currency). */
    targetOffer: number | null
    /** Field 337, "Target UOM" (text). */
    targetUom: string | null
    /** Field 339, "Trucks / Week" (numeric). */
    trucksPerWeek: number | null
    /** Field 340, "Target FR/UOM" (numeric). */
    targetFreightPerUom: number | null
    /** Field 341 (currency). */
    targetFreightCost: number | null
    /** Field 346 (currency). */
    targetConsumerPrice: number | null
    /** Field 345 (currency). */
    priceInNetTons: number | null
    /** Field 347, "Price in LBS" (currency). */
    priceInLbs: number | null
    /** Field 348 (currency). */
    priceInCwt: number | null
    /** Field 349 (currency). */
    priceInGrossTons: number | null
    /** Field 358, "Target PO Number" (text). */
    targetPoNumber: string | null
    /** Field 363 (currency). */
    priceChangeFromPrior: number | null
}

export interface SupplierTargetPricingProgressRailResponse {
    targetPricingProgressRail: SupplierTargetPricingProgressRail
    freshness: DataFreshness
}

/** One contact for a supplier (Quickbase Contacts table). Null means empty in Quickbase. */
export interface SupplierContact {
    /** Quickbase Record ID# (field 3). */
    recordId: number
    /** Field 29, the supplier's record id this contact belongs to. */
    relatedSupplier: number | null
    /** Field 20. */
    lastName: string | null
    /** Field 18. */
    firstName: string | null
    /** Field 25. */
    title: string | null
    /** Field 22. */
    phone: string | null
    /** Field 15. */
    cell: string | null
    /** Field 17. */
    email: string | null
    /** Field 21. */
    notes: string | null
    /** Field 46, "Main?". The main contact is returned first. */
    isMain: boolean | null
}

export interface SupplierContactsResponse {
    supplierRecordId: number
    contacts: SupplierContact[]
    freshness: DataFreshness
}

/**
 * One in-stock item for a supplier (Quickbase In-Stock Items table). Currency
 * and numeric fields are decimals; null means empty. `packaging` and `prep` are
 * the same Quickbase field (10) and always carry the same value.
 */
export interface SupplierInStockItem {
    /** Quickbase Record ID# (field 3), the item's id. */
    recordId: number
    /** Field 15, the supplier's record id this item belongs to. */
    relatedSupplier: number | null
    /** Field 6. Use the material-types catalog for a label. */
    materialType: string | null
    /** Field 10 (Packaging). Use the preparations catalog for a label. */
    packaging: string | null
    /** Field 10 (Prep), the same value as `packaging`. */
    prep: string | null
    /** Field 7. */
    weight: number | null
    /** Field 8. Use the units-of-measure catalog for a label. */
    unitOfMeasure: string | null
    /** Field 14 (currency). */
    targetOffer: number | null
    /** Field 18. Use the truck-types catalog for a label. */
    truckType: string | null
    /** Field 20. */
    trucksPerWeek: number | null
    /** Field 19. */
    volume: number | null
}

export interface SupplierInStockItemsResponse {
    supplierRecordId: number
    items: SupplierInStockItem[]
    freshness: DataFreshness
}

/** One call note for a supplier (Quickbase Call Notes table). Newest first. */
export interface SupplierCallNote {
    /** Quickbase Record ID# (field 3), the note's id. */
    recordId: number
    /** Field 1 (date/time, UTC). */
    dateCreated: string | null
    /** Field 17. */
    notes: string | null
    /** Field 19, the supplier's record id this note belongs to. */
    relatedSupplier: number | null
}

export interface SupplierCallNotesHistoryResponse {
    supplierRecordId: number
    notes: SupplierCallNote[]
    freshness: DataFreshness
}

/**
 * One ITS dispatch record linked to a supplier (Quickbase ITS Dispatch Records
 * table). Null means empty in Quickbase. Dates are `yyyy-MM-dd`.
 */
export interface SupplierDispatchRecord {
    /** Quickbase Record ID# (field 3), the dispatch record's id. */
    recordId: number
    /** Field 6, "Load #". */
    loadNumber: string | null
    /** Field 241 with 432 and 433, formatted as "Consumer - City, State" (empty parts left out). */
    consumer: string | null
    /** Field 432. */
    consumerCity: string | null
    /** Field 433. */
    consumerState: string | null
    /** Field 28, "Carrier/Driver". */
    carrierDriver: string | null
    /** Field 508. */
    material: string | null
    /** Field 378. */
    weight: number | null
    /** Field 14, `yyyy-MM-dd`. */
    shipDate: string | null
    /** Field 15, "Del Date", `yyyy-MM-dd`. */
    deliveryDate: string | null
    /** Field 8. Use the dispatch-statuses catalog for a label; null when empty or unknown. */
    status: string | null
}

/** A supplier's linked ITS dispatch records, newest ship date first. Empty when there are none. */
export interface SupplierDispatchRecordsResponse {
    supplierRecordId: number
    dispatchRecords: SupplierDispatchRecord[]
    freshness: DataFreshness
}

/** One static dropdown option: the `value` a supplier response carries, and the `label` to show. */
export interface DropdownOption {
    value: string
    label: string
}

/* -------------------------------------------------------------------------- */
/*  Reads                                                                      */
/* -------------------------------------------------------------------------- */

/** The caller's suppliers with a name, sorted by name, one page at a time. */
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

/** One supplier by its Quickbase Record ID# (404 when it isn't in the caller's scope). */
export function getSupplier(recordId: number): Promise<SupplierResponse> {
    return apiClient.fetchWithAuth<SupplierResponse>(`/api/suppliers/${recordId}`)
}

/** A supplier's call and prospect status. */
export function getSupplierCallProspectStatus(
    recordId: number
): Promise<SupplierCallProspectStatusResponse> {
    return apiClient.fetchWithAuth<SupplierCallProspectStatusResponse>(
        `/api/suppliers/${recordId}/call-prospect-status`
    )
}

/** A supplier's yard capabilities (all yes/no). */
export function getSupplierYardCapabilities(
    recordId: number
): Promise<SupplierYardCapabilitiesResponse> {
    return apiClient.fetchWithAuth<SupplierYardCapabilitiesResponse>(
        `/api/suppliers/${recordId}/yard-capabilities`
    )
}

/** A supplier's Target Pricing — Progress Rail. */
export function getSupplierTargetPricingProgressRail(
    recordId: number
): Promise<SupplierTargetPricingProgressRailResponse> {
    return apiClient.fetchWithAuth<SupplierTargetPricingProgressRailResponse>(
        `/api/suppliers/${recordId}/target-pricing-progress-rail`
    )
}

/** A supplier's contacts, main contact first. Empty when it has none. */
export function getSupplierContacts(recordId: number): Promise<SupplierContactsResponse> {
    return apiClient.fetchWithAuth<SupplierContactsResponse>(
        `/api/suppliers/${recordId}/contacts`
    )
}

/** A supplier's in-stock items. Empty when it has none. */
export function getSupplierInStockItems(
    recordId: number
): Promise<SupplierInStockItemsResponse> {
    return apiClient.fetchWithAuth<SupplierInStockItemsResponse>(
        `/api/suppliers/${recordId}/in-stock-items`
    )
}

/** A supplier's call notes history, newest first. Empty when it has none. */
export function getSupplierCallNotesHistory(
    recordId: number
): Promise<SupplierCallNotesHistoryResponse> {
    return apiClient.fetchWithAuth<SupplierCallNotesHistoryResponse>(
        `/api/suppliers/${recordId}/call-notes-history`
    )
}

/** A supplier's linked ITS dispatch records, newest ship date first. Empty when there are none. */
export function getSupplierLinkedDispatchRecords(
    recordId: number
): Promise<SupplierDispatchRecordsResponse> {
    return apiClient.fetchWithAuth<SupplierDispatchRecordsResponse>(
        `/api/suppliers/${recordId}/linked-its-dispatch-records`
    )
}

/* -------------------------------------------------------------------------- */
/*  Dropdown catalogs (static reference data; any signed-in user)             */
/* -------------------------------------------------------------------------- */

/** Payment terms options. */
export function listPaymentTerms(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/payment-terms')
}

/** Dead freight options. */
export function listDeadFreight(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/dead-freight')
}

/** Last call result options. */
export function listLastCallResults(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/last-call-results')
}

/** Supplier objection options. */
export function listSupplierObjections(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/supplier-objections')
}

/** In-stock item material type options. */
export function listMaterialTypes(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/in-stock-items/material-types')
}

/** In-stock item unit-of-measure options. */
export function listUnitsOfMeasure(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>(
        '/api/suppliers/in-stock-items/units-of-measure'
    )
}

/** In-stock item preparation (packaging / prep) options. */
export function listPreparations(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/in-stock-items/preparations')
}

/** In-stock item truck type options. */
export function listTruckTypes(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>('/api/suppliers/in-stock-items/truck-types')
}

/** ITS dispatch record status options, in order. */
export function listDispatchStatuses(): Promise<DropdownOption[]> {
    return apiClient.fetchWithAuth<DropdownOption[]>(
        '/api/suppliers/linked-its-dispatch-records/statuses'
    )
}
