// Typed wrappers around the ScrapGo.Core.Api admin endpoints.
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useAdminQueries` for hooks.

import { apiClient } from './api-client'
import type { PagedResult } from './types'

/* -------------------------------------------------------------------------- */
/*  Types                                                                     */
/* -------------------------------------------------------------------------- */

/* -------------------------------------------------------------------------- */
/*  DTOs (mirror the backend contracts)                                       */
/* -------------------------------------------------------------------------- */

export interface OrganizationSummary {
    id: number
    name: string
    slug: string
    status: string
}

export interface CreatedOrganization {
    id: number
}

export interface Permission {
    name: string
}

export interface Role {
    id: number
    name: string
    description: string
}

export interface RoleDetail extends Role {
    permissions: string[]
}

export interface AttachPermissionResult {
    attached: boolean
    permission: string
}

export interface LinkProviderResult {
    linked: boolean
    provider: string
}

// User DTOs
export interface UserSummary {
    id: number
    email: string
    displayName: string | null
    status: 'Active' | 'Disabled'
    createdAt: string
}

export interface UserDetail extends UserSummary {
    roles: { roleId: number; name: string; organizationId: number | null }[]
}

// Organization DTOs
export interface OrganizationDetail {
    id: number
    name: string
    slug: string
    status: string
    createdAt: string
    activeMemberCount: number
}

export interface OrganizationMember {
    userId: number
    email: string
    displayName: string | null
    joinedAt: string
    roles: { roleId: number; name: string; organizationId: number | null }[]
}

export interface AssignedRoleDto {
    roleId: number
    name: string
    organizationId: number | null
}

/* -------------------------------------------------------------------------- */
/*  Organizations                                                             */
/* -------------------------------------------------------------------------- */

export function listOrganizations(): Promise<OrganizationSummary[]> {
    return apiClient.fetchWithAuth<OrganizationSummary[]>('/api/organizations')
}

export function createOrganization(name: string): Promise<CreatedOrganization> {
    return apiClient.fetchWithAuth<CreatedOrganization>('/api/organizations', {
        method: 'POST',
        body: JSON.stringify({ name }),
    })
}

// Organization detail endpoints
export function getOrganizationDetail(organizationId: number): Promise<OrganizationDetail> {
    return apiClient.fetchWithAuth<OrganizationDetail>(`/api/organizations/${organizationId}`)
}

export function updateOrganization(organizationId: number, name: string): Promise<OrganizationDetail> {
    return apiClient.fetchWithAuth<OrganizationDetail>(`/api/organizations/${organizationId}`, {
        method: 'PUT',
        body: JSON.stringify({ name }),
    })
}

export function listOrganizationMembers(
    organizationId: number,
    page: number = 1,
    pageSize: number = 25
): Promise<PagedResult<OrganizationMember>> {
    return apiClient.fetchWithAuth<PagedResult<OrganizationMember>>(
        `/api/organizations/${organizationId}/members?page=${page}&pageSize=${pageSize}`
    )
}

export function addOrganizationMember(organizationId: number, userId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/organizations/${organizationId}/members/${userId}`, {
        method: 'POST',
    })
}

export function removeOrganizationMember(organizationId: number, userId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/organizations/${organizationId}/members/${userId}`, {
        method: 'DELETE',
    })
}

/* -------------------------------------------------------------------------- */
/*  Permissions                                                                */
/* -------------------------------------------------------------------------- */

export function listPermissions(): Promise<Permission[]> {
    return apiClient.fetchWithAuth<Permission[]>('/api/permissions')
}

/* -------------------------------------------------------------------------- */
/*  Roles                                                                     */
/* -------------------------------------------------------------------------- */

// Organization-scoped role endpoints
export function listOrganizationRoles(organizationId: number): Promise<Role[]> {
    return apiClient.fetchWithAuth<Role[]>(`/api/organizations/${organizationId}/roles`)
}

export function getOrganizationRole(organizationId: number, roleId: number): Promise<RoleDetail> {
    return apiClient.fetchWithAuth<RoleDetail>(`/api/organizations/${organizationId}/roles/${roleId}`)
}

export function getOrganizationRolePermissions(organizationId: number, roleId: number): Promise<{ name: string }[]> {
    return apiClient.fetchWithAuth<{ name: string }[]>(`/api/organizations/${organizationId}/roles/${roleId}/permissions`)
}

// Role management endpoints
export function createRole(input: {
    organizationId: number
    name: string
    description?: string
}): Promise<Role> {
    return apiClient.fetchWithAuth<Role>('/api/roles', {
        method: 'POST',
        body: JSON.stringify(input),
    })
}

export function updateRole(
    id: number,
    input: { name: string; description?: string }
): Promise<Role> {
    return apiClient.fetchWithAuth<Role>(`/api/roles/${id}`, {
        method: 'PUT',
        body: JSON.stringify(input),
    })
}

export function deleteRole(id: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/roles/${id}`, { method: 'DELETE' })
}

export function attachPermission(
    roleId: number,
    permissionName: string
): Promise<AttachPermissionResult> {
    return apiClient.fetchWithAuth<AttachPermissionResult>(
        `/api/roles/${roleId}/permissions`,
        {
            method: 'POST',
            body: JSON.stringify({ permissionName }),
        }
    )
}

export function detachPermission(roleId: number, permissionName: string): Promise<void> {
    return apiClient.fetchWithAuth<void>(
        `/api/roles/${roleId}/permissions/${encodeURIComponent(permissionName)}`,
        { method: 'DELETE' }
    )
}

/* -------------------------------------------------------------------------- */
/*  Users (Platform Admin)                                                    */
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

/* -------------------------------------------------------------------------- */
/*  Role Assignment                                                           */
/* -------------------------------------------------------------------------- */

export interface AssignRoleRequest {
    roleId: number
    organizationId?: number
}

export interface AssignedRoleDto {
    roleId: number
    name: string
    organizationId: number | null
}

export function assignRole(userId: number, request: AssignRoleRequest): Promise<AssignedRoleDto> {
    return apiClient.fetchWithAuth<AssignedRoleDto>(`/api/users/${userId}/roles`, {
        method: 'POST',
        body: JSON.stringify(request),
    })
}

export function revokeRole(userId: number, roleId: number, organizationId?: number): Promise<void> {
    const params = new URLSearchParams()
    if (organizationId !== undefined) {
        params.append('organizationId', organizationId.toString())
    }

    return apiClient.fetchWithAuth<void>(`/api/users/${userId}/roles/${roleId}?${params.toString()}`, {
        method: 'DELETE',
    })
}

/* -------------------------------------------------------------------------- */
/*  Self                                                                      */
/* -------------------------------------------------------------------------- */

export function linkProvider(reauthIdToken: string): Promise<LinkProviderResult> {
    return apiClient.fetchWithAuth<LinkProviderResult>(
        '/api/users/me/linked-providers/link',
        {
            method: 'POST',
            body: JSON.stringify({ reauthIdToken }),
        }
    )
}
