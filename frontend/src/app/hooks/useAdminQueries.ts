import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
    addOrganizationMember,
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
    getOrganizationRolePermissions,
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
    })
}

export function useOrganizationRoleDetail(organizationId: number, roleId: number) {
    return useQuery({
        queryKey: adminKeys.roleDetail(organizationId, roleId),
        queryFn: () => getOrganizationRole(organizationId, roleId),
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
        mutationFn: (name: string) => createOrganization(name),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.organizations })
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
