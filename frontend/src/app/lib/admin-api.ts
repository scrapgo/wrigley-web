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
    /** Set when the first administrator was named by email: show the token once. */
    invitation?: { invitationId: number; token: string; expiresAt: string } | null
}

export interface Permission {
    name: string
}

export interface Role {
    id: number
    name: string
    description: string
    organizationId: number | null
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

export type CatalogStatus = 'Active' | 'Retired'

export interface CatalogModule {
    id: number
    key: string
    name: string
    status: CatalogStatus
    permissions: string[]
}

export interface CatalogApplication {
    id: number
    key: string
    name: string
    status: CatalogStatus
    modules: CatalogModule[]
    permissions: string[]
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

/** An application assigned to an organization, with its enabled modules only. */
export interface OrganizationApplication {
    applicationId: number
    key: string
    name: string
    modules: { moduleId: number; key: string; name: string }[]
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

/** Platform administrators: all organizations, paged. */
export function listAllOrganizations(
    search?: string,
    status?: string,
    page: number = 1,
    pageSize: number = 25
): Promise<PagedResult<OrganizationSummary>> {
    const params = new URLSearchParams()
    if (search) params.append('search', search)
    if (status) params.append('status', status)
    params.append('page', page.toString())
    params.append('pageSize', pageSize.toString())

    return apiClient.fetchWithAuth<PagedResult<OrganizationSummary>>(`/api/admin/organizations?${params.toString()}`)
}

export function createOrganization(
    input: { name: string; firstAdminUserId: number } | { name: string; firstAdminEmail: string }
): Promise<CreatedOrganization> {
    return apiClient.fetchWithAuth<CreatedOrganization>('/api/admin/organizations', {
        method: 'POST',
        body: JSON.stringify(input),
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

/** Platform administrators: rename an organization. */
export function renameOrganizationAsAdmin(organizationId: number, name: string): Promise<OrganizationDetail> {
    return apiClient.fetchWithAuth<OrganizationDetail>(`/api/admin/organizations/${organizationId}`, {
        method: 'PUT',
        body: JSON.stringify({ name }),
    })
}

/** Platform administrators: make an existing user member + administrator; revokes pending invitations. */
export function setOrganizationAdministrator(organizationId: number, userId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}/administrators/${userId}`, {
        method: 'PUT',
    })
}

/** Platform administrators: permanently delete a deactivated organization and everything in it. */
export function deleteOrganization(organizationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}`, { method: 'DELETE' })
}

/** Platform administrators: deactivate an organization. */
export function deactivateOrganization(organizationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}/deactivate`, { method: 'POST' })
}

/** Platform administrators: reactivate an organization. */
export function reactivateOrganization(organizationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}/reactivate`, { method: 'POST' })
}

/** Platform administrators: the existing user with exactly this email, if any. */
export async function findUserByEmail(email: string): Promise<UserSummary | undefined> {
    const page = await listUsers(email, undefined, 1, 25)
    return page.items.find((u) => u.email.toLowerCase() === email.toLowerCase())
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

/** One member's grants in an organization: org-level and per application. */
export function listMemberGrants(organizationId: number, userId: number): Promise<MemberGrant[]> {
    return apiClient.fetchWithAuth<MemberGrant[]>(`/api/organizations/${organizationId}/members/${userId}/grants`)
}

/** Platform administrators: assign an application to an organization. */
export function assignApplication(organizationId: number, applicationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}/applications/${applicationId}`, {
        method: 'PUT',
    })
}

/** Platform administrators: remove an application from an organization. */
export function removeApplication(organizationId: number, applicationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/admin/organizations/${organizationId}/applications/${applicationId}`, {
        method: 'DELETE',
    })
}

/** Platform administrators: enable a module for an organization's application. */
export function enableModule(organizationId: number, applicationId: number, moduleId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(
        `/api/admin/organizations/${organizationId}/applications/${applicationId}/modules/${moduleId}`,
        { method: 'PUT' }
    )
}

/** Platform administrators: disable a module for an organization's application. */
export function disableModule(organizationId: number, applicationId: number, moduleId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(
        `/api/admin/organizations/${organizationId}/applications/${applicationId}/modules/${moduleId}`,
        { method: 'DELETE' }
    )
}

/** Platform administrators: the organization's assigned applications and enabled modules. */
export function listOrganizationApplicationsAsAdmin(organizationId: number): Promise<OrganizationApplication[]> {
    return apiClient.fetchWithAuth<OrganizationApplication[]>(`/api/admin/organizations/${organizationId}/applications`)
}

/* -------------------------------------------------------------------------- */
/*  Catalog                                                                   */
/* -------------------------------------------------------------------------- */

/** The application catalog: applications → modules → permissions. Any signed-in user. */
export function listCatalog(): Promise<CatalogApplication[]> {
    return apiClient.fetchWithAuth<CatalogApplication[]>('/api/catalog/applications')
}

/** Retire or reactivate an application platform-wide (`Catalog.Manage`). */
export function setCatalogApplicationStatus(applicationId: number, status: CatalogStatus): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/catalog/applications/${applicationId}`, {
        method: 'PUT',
        body: JSON.stringify({ status }),
    })
}

/** Retire or reactivate a module platform-wide (`Catalog.Manage`). */
export function setCatalogModuleStatus(applicationId: number, moduleId: number, status: CatalogStatus): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/catalog/applications/${applicationId}/modules/${moduleId}`, {
        method: 'PUT',
        body: JSON.stringify({ status }),
    })
}

/* -------------------------------------------------------------------------- */
/*  Application Roles                                                          */
/* -------------------------------------------------------------------------- */

const adminAppPath = (organizationId: number, applicationId: number) =>
    `/api/admin/organizations/${organizationId}/applications/${applicationId}`
const orgAppPath = (organizationId: number, applicationId: number) =>
    `/api/organizations/${organizationId}/applications/${applicationId}`

export interface ApplicationRole extends RoleDetail {
    /** Set for application roles; organizationId null means a read-only template. */
    applicationId: number | null
}

export interface ApplicationGrant {
    roleId: number
    name: string
    organizationId: number | null
    applicationId: number | null
    expiresAt: string | null
}

/** One member's access to an application: their grants and what they resolve to now. */
export interface AccessReviewEntry {
    userId: number
    email: string
    roles: ApplicationGrant[]
    permissions: string[]
}

/** Platform administrators: the application's roles in an organization (templates first). */
export function listApplicationRolesAsAdmin(organizationId: number, applicationId: number): Promise<ApplicationRole[]> {
    return apiClient.fetchWithAuth<ApplicationRole[]>(`${adminAppPath(organizationId, applicationId)}/roles`)
}

/** Platform administrators: grant an application role to a member, e.g. the first application administrator. */
export function grantApplicationRoleAsAdmin(
    organizationId: number,
    applicationId: number,
    userId: number,
    roleId: number
): Promise<ApplicationGrant> {
    return apiClient.fetchWithAuth<ApplicationGrant>(
        `${adminAppPath(organizationId, applicationId)}/members/${userId}/roles/${roleId}`,
        { method: 'PUT', body: JSON.stringify({}) }
    )
}

/** Members with User.Read: the organization's applications and enabled modules. */
export function listOrganizationApplications(organizationId: number): Promise<OrganizationApplication[]> {
    return apiClient.fetchWithAuth<OrganizationApplication[]>(`/api/organizations/${organizationId}/applications`)
}

/** Application administrators: the application's roles here (templates first, then custom roles). */
export function listApplicationRoles(organizationId: number, applicationId: number): Promise<ApplicationRole[]> {
    return apiClient.fetchWithAuth<ApplicationRole[]>(`${orgAppPath(organizationId, applicationId)}/roles`)
}

/** Application administrators: who has access to the application and what it resolves to. */
export function reviewApplicationAccess(organizationId: number, applicationId: number): Promise<AccessReviewEntry[]> {
    return apiClient.fetchWithAuth<AccessReviewEntry[]>(`${orgAppPath(organizationId, applicationId)}/access`)
}

/** Application administrators: grant a role to a member, optionally expiring. Idempotent. */
export function grantApplicationRole(
    organizationId: number,
    applicationId: number,
    userId: number,
    roleId: number,
    expiresAt?: string
): Promise<ApplicationGrant> {
    return apiClient.fetchWithAuth<ApplicationGrant>(
        `${orgAppPath(organizationId, applicationId)}/members/${userId}/roles/${roleId}`,
        { method: 'PUT', body: JSON.stringify(expiresAt ? { expiresAt } : {}) }
    )
}

/** Application administrators: revoke a member's application role. */
export function revokeApplicationRole(
    organizationId: number,
    applicationId: number,
    userId: number,
    roleId: number
): Promise<void> {
    return apiClient.fetchWithAuth<void>(`${orgAppPath(organizationId, applicationId)}/members/${userId}/roles/${roleId}`, {
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

export interface MemberGrant {
    roleId: number
    name: string
    organizationId: number | null
    applicationId: number | null
    expiresAt: string | null
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
/*  Invitations                                                               */
/* -------------------------------------------------------------------------- */

export interface Invitation {
    id: number
    email: string
    status: 'Pending' | 'Accepted' | 'Revoked' | 'Expired'
    expiresAt: string
    invitedByUserId: number
    invitedByUserEmail: string
}

export interface InvitationGrant {
    roleId: number
    applicationId: number | null
}

/** Invite a user to an organization by email with pre-granted roles. */
export function createInvitation(
    organizationId: number,
    email: string,
    grants: InvitationGrant[],
    expiresInDays?: number
): Promise<Invitation> {
    return apiClient.fetchWithAuth<Invitation>(`/api/organizations/${organizationId}/invitations`, {
        method: 'POST',
        body: JSON.stringify({ email, grants, expiresInDays }),
    })
}

/** List pending invitations for an organization. */
export function listInvitations(organizationId: number): Promise<Invitation[]> {
    return apiClient.fetchWithAuth<Invitation[]>(`/api/organizations/${organizationId}/invitations`)
}

/** Revoke a pending invitation. */
export function revokeInvitation(organizationId: number, invitationId: number): Promise<void> {
    return apiClient.fetchWithAuth<void>(`/api/organizations/${organizationId}/invitations/${invitationId}`, {
        method: 'DELETE',
    })
}

/** Accept an invitation by token. The signed-in user's email must match and be verified. */
export function acceptInvitation(token: string): Promise<{ organizationId: number; grantedCount: number; skippedGrants: number }> {
    return apiClient.fetchWithAuth(`/api/invitations/accept`, {
        method: 'POST',
        body: JSON.stringify({ token }),
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
