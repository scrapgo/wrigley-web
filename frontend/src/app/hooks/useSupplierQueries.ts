import { useQuery } from '@tanstack/react-query'

import { getSupplier, listSuppliers } from '../lib/supplier-api'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const supplierKeys = {
    list: (search: string, skip: number, top: number) =>
        ['suppliers', 'list', search, skip, top] as const,
    detail: (recordId: number) => ['suppliers', 'detail', recordId] as const,
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
