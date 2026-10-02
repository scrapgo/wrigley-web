// Typed wrappers around the ScrapGo.Core.Api admin endpoints that exist today.
//
// Every call goes through `apiClient.fetchWithAuth`, which attaches the bearer
// token and throws `ApiError` (with the backend's ProblemDetails `reason`) on
// failure. This module holds no React state — see `useAdminQueries` for hooks.

import { apiClient } from './api-client'

/* -------------------------------------------------------------------------- */
/*  DTOs (mirror the backend contracts)                                        */
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

export interface AttachPermissionResult {
    attached: boolean
    permission: string
}

export interface LinkProviderResult {
    linked: boolean
    provider: string
}

/* -------------------------------------------------------------------------- */
/*  Organizations                                                              */
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

/* -------------------------------------------------------------------------- */
/*  Permissions                                                                */
/* -------------------------------------------------------------------------- */

export function listPermissions(): Promise<Permission[]> {
    return apiClient.fetchWithAuth<Permission[]>('/api/permissions')
}

/* -------------------------------------------------------------------------- */
/*  Roles                                                                      */
/* -------------------------------------------------------------------------- */

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
/*  Self                                                                       */
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
