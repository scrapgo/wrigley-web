// Typed wrappers around the ScrapGo.Core.Api admin endpoints.
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useAdminQueries` for hooks.

import { apiClient } from './api-client'
import type { PagedResult } from './types'

/* -------------------------------------------------------------------------- */
/*  DTOs (mirror the backend contracts)                                       */
/* -------------------------------------------------------------------------- */

export interface Permission {
    name: string
}

export interface UserSummary {
    id: number
    email: string
    displayName: string | null
    status: 'Active' | 'Disabled'
    createdAt: string
}

export interface UserDetail extends UserSummary {
    roles: { roleId: number; name: string }[]
}

/* -------------------------------------------------------------------------- */
/*  Permissions                                                                */
/* -------------------------------------------------------------------------- */

export function listPermissions(): Promise<Permission[]> {
    return apiClient.fetchWithAuth<Permission[]>('/api/permissions')
}

/* -------------------------------------------------------------------------- */
/*  Users                                                                      */
/* -------------------------------------------------------------------------- */

export function listUsers(
    search?: string,
    status?: string,
    page: number = 1,
    pageSize: number = 25
): Promise<PagedResult<UserSummary>> {
    const params = new URLSearchParams()
    if (search) params.append('search', search)
    if (status) params.append('status', status)
    params.append('page', page.toString())
    params.append('pageSize', pageSize.toString())

    return apiClient.fetchWithAuth<PagedResult<UserSummary>>(`/api/users?${params.toString()}`)
}

export function getUser(id: number): Promise<UserDetail> {
    return apiClient.fetchWithAuth<UserDetail>(`/api/users/${id}`)
}

export function disableUser(id: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/users/${id}/disable`, {
        method: 'POST',
    })
}

export function enableUser(id: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/users/${id}/enable`, {
        method: 'POST',
    })
}
