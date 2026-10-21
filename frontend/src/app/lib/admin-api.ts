// Typed wrappers around the ScrapGo.Core.Api admin endpoints (/api/admin/…).
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useAdminQueries` for hooks.

import { apiClient } from './api-client'
import type { PagedResult } from './types'

/* -------------------------------------------------------------------------- */
/*  DTOs (mirror the backend contracts)                                       */
/* -------------------------------------------------------------------------- */

export type UserType = 'Internal' | 'External'

export type UserStatus = 'Active' | 'Disabled'

export interface Permission {
    name: string
}

export interface UserSummary {
    id: number
    email: string
    displayName: string | null
    userType: UserType
    status: UserStatus
    lastSignInAt: string | null
    createdAt: string
    /** Role keys, e.g. `scrapgo_user`. */
    roles: string[]
}

export interface UserRole {
    id: number
    key: string
    name: string
}

export interface UserDetail {
    id: number
    identityPlatformUid: string
    email: string
    displayName: string | null
    userType: UserType
    status: UserStatus
    signInProvider: string | null
    lastSignInAt: string | null
    createdAt: string
    roles: UserRole[]
    /** Linked Quickbase supplier Record IDs (External users only). */
    supplierRecordIds: number[]
}

export interface Role {
    id: number
    key: string
    name: string
    description: string | null
    userType: UserType
    isSystem: boolean
    /** Permission keys the role grants. */
    permissions: string[]
    holderCount: number
}

export interface UserListQuery {
    search?: string
    type?: UserType
    status?: UserStatus
    page: number
    pageSize: number
}

/* -------------------------------------------------------------------------- */
/*  Permissions                                                                */
/* -------------------------------------------------------------------------- */

export function listPermissions(): Promise<Permission[]> {
    return apiClient.fetchWithAuth<Permission[]>('/api/permissions')
}

/* -------------------------------------------------------------------------- */
/*  Users (users.view to read, users.manage to change)                         */
/* -------------------------------------------------------------------------- */

export function listUsers(query: UserListQuery): Promise<PagedResult<UserSummary>> {
    const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) })
    if (query.search) params.set('search', query.search)
    if (query.type) params.set('type', query.type)
    if (query.status) params.set('status', query.status)

    return apiClient.fetchWithAuth<PagedResult<UserSummary>>(`/api/admin/users?${params.toString()}`)
}

export function getUser(id: number): Promise<UserDetail> {
    return apiClient.fetchWithAuth<UserDetail>(`/api/admin/users/${id}`)
}

export function disableUser(id: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/users/${id}/disable`, { method: 'POST' })
}

export function enableUser(id: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/users/${id}/enable`, { method: 'POST' })
}

/** Replaces the user's roles with exactly these (same user type only). */
export function setUserRoles(id: number, roleIds: number[]): Promise<UserDetail> {
    return apiClient.fetchWithAuth<UserDetail>(`/api/admin/users/${id}/roles`, {
        method: 'PUT',
        body: JSON.stringify({ roleIds }),
    })
}

/** Replaces an External user's supplier links with exactly these Record IDs. */
export function setUserSuppliers(id: number, supplierRecordIds: number[]): Promise<UserDetail> {
    return apiClient.fetchWithAuth<UserDetail>(`/api/admin/users/${id}/suppliers`, {
        method: 'PUT',
        body: JSON.stringify({ supplierRecordIds }),
    })
}

/* -------------------------------------------------------------------------- */
/*  Roles (roles.view)                                                         */
/* -------------------------------------------------------------------------- */

export function listRoles(): Promise<Role[]> {
    return apiClient.fetchWithAuth<Role[]>('/api/admin/roles')
}
