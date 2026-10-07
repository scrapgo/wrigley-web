import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
    addOrganizationMember,
    grantApplicationRole,
    grantApplicationRoleAsAdmin,
    listApplicationRoles,
    listApplicationRolesAsAdmin,
    listOrganizationApplications,
    reviewApplicationAccess,
    revokeApplicationRole,
    assignApplication,
    deactivateOrganization,
    deleteOrganization,
    renameOrganizationAsAdmin,
    setOrganizationAdministrator,
    disableModule,
    enableModule,
    listAllOrganizations,
    listCatalog,
    listOrganizationApplicationsAsAdmin,
    reactivateOrganization,
    removeApplication,
    setCatalogApplicationStatus,
    setCatalogModuleStatus,
    type CatalogStatus,
    assignRole,
    attachPermission,
    createOrganization,
    createRole,
    deleteRole,
    detachPermission,
    disableUser,
    enableUser,
    getOrganizationDetail,
    getOrganizationRole,
    listOrganizationMembers,
    listOrganizationRoles,
    listOrganizations,
    listPermissions,
    listUsers,
    removeOrganizationMember,
    revokeRole,
    updateOrganization,
    updateRole,
    getUser,
} from '../lib/admin-api'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const adminKeys = {
    organizations: ['admin', 'organizations'] as const,
    organizationDetail: (id: number) => ['admin', 'organization', id] as const,
    organizationMembers: (id: number) => ['admin', 'organization', id, 'members'] as const,
    permissions: ['admin', 'permissions'] as const,
    roles: (organizationId: number) => ['admin', 'roles', organizationId] as const,
    roleDetail: (organizationId: number, roleId: number) => ['admin', 'role', organizationId, roleId] as const,
    users: ['admin', 'users'] as const,
    userDetail: (id: number) => ['admin', 'user', id] as const,
    allOrganizations: (search: string, status: string) => ['admin', 'all-organizations', search, status] as const,
    catalog: ['admin', 'catalog'] as const,
    organizationApplications: (id: number) => ['admin', 'organization', id, 'applications'] as const,
    memberOrganizationApplications: (id: number) => ['org', id, 'applications'] as const,
    applicationRoles: (orgId: number, appId: number, asAdmin: boolean) => ['app', orgId, appId, 'roles', asAdmin] as const,
    applicationAccess: (orgId: number, appId: number) => ['app', orgId, appId, 'access'] as const,
}

/* -------------------------------------------------------------------------- */
/*  Queries                                                                    */
/* -------------------------------------------------------------------------- */

export function useOrganizations() {
    return useQuery({
        queryKey: adminKeys.organizations,
        queryFn: listOrganizations,
    })
}

/** Platform administrators: every organization (first page of 100). */
export function useAllOrganizations(search: string, status: string, enabled: boolean) {
    return useQuery({
        queryKey: adminKeys.allOrganizations(search, status),
        queryFn: () => listAllOrganizations(search || undefined, status || undefined, 1, 100),
        enabled,
    })
}

export function useCatalog() {
    return useQuery({
        queryKey: adminKeys.catalog,
        queryFn: listCatalog,
    })
}

export function useOrganizationApplicationsAsAdmin(organizationId: number) {
    return useQuery({
        queryKey: adminKeys.organizationApplications(organizationId),
        queryFn: () => listOrganizationApplicationsAsAdmin(organizationId),
        enabled: organizationId > 0,
    })
}

export function useOrganizationDetail(organizationId: number) {
    return useQuery({
        queryKey: adminKeys.organizationDetail(organizationId),
        queryFn: () => getOrganizationDetail(organizationId),
    })
}

export function useOrganizationMembers(organizationId: number, page: number = 1, pageSize: number = 25) {
    return useQuery({
        queryKey: adminKeys.organizationMembers(organizationId),
        queryFn: () => listOrganizationMembers(organizationId, page, pageSize),
    })
}

export function usePermissions() {
    return useQuery({
        queryKey: adminKeys.permissions,
        queryFn: listPermissions,
    })
}

/**
 * Roles come from the server API now.
 */
export function useOrganizationRoles(organizationId: number) {
    return useQuery({
        queryKey: adminKeys.roles(organizationId),
        queryFn: () => listOrganizationRoles(organizationId),
        enabled: organizationId > 0,
    })
}

export function useOrganizationRoleDetail(organizationId: number, roleId: number) {
    return useQuery({
        queryKey: adminKeys.roleDetail(organizationId, roleId),
        queryFn: () => getOrganizationRole(organizationId, roleId),
        enabled: organizationId > 0 && roleId > 0,
    })
}

export function useUsers(search?: string, status?: string, page: number = 1, pageSize: number = 25) {
    return useQuery({
        queryKey: adminKeys.users,
        queryFn: () => listUsers(search, status, page, pageSize),
    })
}

export function useUserDetail(id: number) {
    return useQuery({
        queryKey: adminKeys.userDetail(id),
        queryFn: () => getUser(id),
    })
}

/* -------------------------------------------------------------------------- */
/*  Mutations                                                                  */
/* -------------------------------------------------------------------------- */

export function useCreateOrganization() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: (input: Parameters<typeof createOrganization>[0]) => createOrganization(input),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizations })
            void queryClient.invalidateQueries({ queryKey: ['admin', 'all-organizations'] })
        },
    })
}

export function useUpdateOrganization() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ organizationId, name }: { organizationId: number; name: string }) =>
            updateOrganization(organizationId, name),
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationDetail(variables.organizationId) })
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizations })
        },
    })
}

export function useAddOrganizationMember() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ organizationId, userId }: { organizationId: number; userId: number }) =>
            addOrganizationMember(organizationId, userId),
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationMembers(variables.organizationId) })
        },
    })
}

export function useRemoveOrganizationMember() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ organizationId, userId }: { organizationId: number; userId: number }) =>
            removeOrganizationMember(organizationId, userId),
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationMembers(variables.organizationId) })
        },
    })
}

export function useCreateRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            organizationId: number
            name: string
            description?: string
        }) => {
            const role = await createRole(input)
            return role
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles(variables.organizationId) })
        },
    })
}

export function useUpdateRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            id: number;
            name: string;
            description?: string;
            organizationId: number
        }) => {
            const role = await updateRole(input.id, {
                name: input.name,
                description: input.description,
            })
            return role
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles(variables.organizationId) })
            void queryClient.invalidateQueries({ queryKey: adminKeys.roleDetail(variables.organizationId, variables.id) })
        },
    })
}

export function useDeleteRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: { id: number; organizationId: number }) => {
            await deleteRole(input.id)
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles(variables.organizationId) })
        },
    })
}

export function useAttachPermission() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            roleId: number;
            permissionName: string;
            organizationId: number;
        }) => {
            const result = await attachPermission(input.roleId, input.permissionName)
            return result
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles(variables.organizationId) })
            void queryClient.invalidateQueries({ queryKey: adminKeys.roleDetail(variables.organizationId, variables.roleId) })
        },
    })
}

export function useDetachPermission() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            roleId: number;
            permissionName: string;
            organizationId: number;
        }) => {
            await detachPermission(input.roleId, input.permissionName)
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles(variables.organizationId) })
            void queryClient.invalidateQueries({ queryKey: adminKeys.roleDetail(variables.organizationId, variables.roleId) })
        },
    })
}

export function useAssignRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            userId: number;
            roleId: number;
            organizationId?: number;
        }) => {
            const result = await assignRole(input.userId, {
                roleId: input.roleId,
                organizationId: input.organizationId
            })
            return result
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.userDetail(variables.userId) })
        },
    })
}

export function useRevokeRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            userId: number;
            roleId: number;
            organizationId?: number;
        }) => {
            await revokeRole(input.userId, input.roleId, input.organizationId)
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.userDetail(variables.userId) })
        },
    })
}

export function useDisableUser() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (id: number) => {
            await disableUser(id)
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.users })
            void queryClient.invalidateQueries({ queryKey: adminKeys.userDetail(variables) })
        },
    })
}

export function useEnableUser() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (id: number) => {
            await enableUser(id)
        },
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.users })
            void queryClient.invalidateQueries({ queryKey: adminKeys.userDetail(variables) })
        },
    })
}

function useInvalidateOrganizations() {
    const queryClient = useQueryClient()
    return () => {
        void queryClient.invalidateQueries({ queryKey: adminKeys.organizations })
        void queryClient.invalidateQueries({ queryKey: ['admin', 'all-organizations'] })
    }
}

export function useSetOrganizationActive() {
    const invalidate = useInvalidateOrganizations()
    return useMutation({
        mutationFn: ({ organizationId, active }: { organizationId: number; active: boolean }) =>
            active ? reactivateOrganization(organizationId) : deactivateOrganization(organizationId),
        onSuccess: invalidate,
    })
}

export function useSetApplicationAssigned() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ organizationId, applicationId, assigned }: { organizationId: number; applicationId: number; assigned: boolean }) =>
            assigned ? assignApplication(organizationId, applicationId) : removeApplication(organizationId, applicationId),
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationApplications(variables.organizationId) })
        },
    })
}

export function useSetModuleEnabled() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({
            organizationId,
            applicationId,
            moduleId,
            enabled,
        }: { organizationId: number; applicationId: number; moduleId: number; enabled: boolean }) =>
            enabled
                ? enableModule(organizationId, applicationId, moduleId)
                : disableModule(organizationId, applicationId, moduleId),
        onSuccess: (_, variables) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationApplications(variables.organizationId) })
        },
    })
}

export function useSetCatalogStatus() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ applicationId, moduleId, status }: { applicationId: number; moduleId?: number; status: CatalogStatus }) =>
            moduleId === undefined
                ? setCatalogApplicationStatus(applicationId, status)
                : setCatalogModuleStatus(applicationId, moduleId, status),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.catalog })
        },
    })
}

/** Members with User.Read: the organization's applications. */
export function useOrganizationApplications(organizationId: number) {
    return useQuery({
        queryKey: adminKeys.memberOrganizationApplications(organizationId),
        queryFn: () => listOrganizationApplications(organizationId),
        enabled: organizationId > 0,
    })
}

/** The application's roles in an organization: through /api/admin for platform admins, else as its administrator. */
export function useApplicationRoles(organizationId: number, applicationId: number, asPlatformAdmin: boolean, enabled = true) {
    return useQuery({
        queryKey: adminKeys.applicationRoles(organizationId, applicationId, asPlatformAdmin),
        queryFn: () =>
            asPlatformAdmin
                ? listApplicationRolesAsAdmin(organizationId, applicationId)
                : listApplicationRoles(organizationId, applicationId),
        enabled: enabled && organizationId > 0 && applicationId > 0,
    })
}

export function useApplicationAccess(organizationId: number, applicationId: number, enabled = true) {
    return useQuery({
        queryKey: adminKeys.applicationAccess(organizationId, applicationId),
        queryFn: () => reviewApplicationAccess(organizationId, applicationId),
        enabled: enabled && organizationId > 0 && applicationId > 0,
    })
}

interface GrantInput {
    organizationId: number
    applicationId: number
    userId: number
    roleId: number
    expiresAt?: string
    asPlatformAdmin?: boolean
}

export function useGrantApplicationRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: (input: GrantInput) =>
            input.asPlatformAdmin
                ? grantApplicationRoleAsAdmin(input.organizationId, input.applicationId, input.userId, input.roleId)
                : grantApplicationRole(input.organizationId, input.applicationId, input.userId, input.roleId, input.expiresAt),
        onSuccess: (_, v) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.applicationAccess(v.organizationId, v.applicationId) })
        },
    })
}

export function useRevokeApplicationRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: (input: Omit<GrantInput, 'expiresAt' | 'asPlatformAdmin'>) =>
            revokeApplicationRole(input.organizationId, input.applicationId, input.userId, input.roleId),
        onSuccess: (_, v) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.applicationAccess(v.organizationId, v.applicationId) })
        },
    })
}

export function useRenameOrganizationAsAdmin() {
    const invalidate = useInvalidateOrganizations()
    return useMutation({
        mutationFn: ({ organizationId, name }: { organizationId: number; name: string }) =>
            renameOrganizationAsAdmin(organizationId, name),
        onSuccess: invalidate,
    })
}

export function useSetOrganizationAdministrator() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: ({ organizationId, userId }: { organizationId: number; userId: number }) =>
            setOrganizationAdministrator(organizationId, userId),
        onSuccess: (_, v) => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizationMembers(v.organizationId) })
        },
    })
}

export function useDeleteOrganization() {
    const invalidate = useInvalidateOrganizations()
    return useMutation({
        mutationFn: (organizationId: number) => deleteOrganization(organizationId),
        onSuccess: invalidate,
    })
}
