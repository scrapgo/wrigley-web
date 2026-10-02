import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
    attachPermission,
    createOrganization,
    createRole,
    deleteRole,
    detachPermission,
    listOrganizations,
    listPermissions,
    updateRole,
} from '../lib/admin-api'
import { roleRegistry } from '../lib/role-registry'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const adminKeys = {
    organizations: ['admin', 'organizations'] as const,
    permissions: ['admin', 'permissions'] as const,
    roles: ['admin', 'roles'] as const,
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

export function usePermissions() {
    return useQuery({
        queryKey: adminKeys.permissions,
        queryFn: listPermissions,
    })
}

/**
 * Roles come from the browser-local registry, not the API — there is no
 * `GET /api/roles`. See `role-registry.ts`.
 */
export function useRegisteredRoles() {
    return useQuery({
        queryKey: adminKeys.roles,
        queryFn: async () => roleRegistry.list(),
        staleTime: Infinity,
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

export function useCreateRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: {
            organizationId: number
            name: string
            description?: string
        }) => {
            const role = await createRole(input)
            roleRegistry.add({
                id: role.id,
                name: role.name,
                description: role.description,
                organizationId: input.organizationId,
            })
            return role
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles })
        },
    })
}

export function useUpdateRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: { id: number; name: string; description?: string }) => {
            const role = await updateRole(input.id, {
                name: input.name,
                description: input.description,
            })
            roleRegistry.update(input.id, {
                name: role.name,
                description: role.description,
            })
            return role
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles })
        },
    })
}

export function useDeleteRole() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (id: number) => {
            await deleteRole(id)
            roleRegistry.remove(id)
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles })
        },
    })
}

export function useAttachPermission() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: { roleId: number; permissionName: string }) => {
            const result = await attachPermission(input.roleId, input.permissionName)
            roleRegistry.addPermission(input.roleId, input.permissionName)
            return result
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles })
        },
    })
}

export function useDetachPermission() {
    const queryClient = useQueryClient()
    return useMutation({
        mutationFn: async (input: { roleId: number; permissionName: string }) => {
            await detachPermission(input.roleId, input.permissionName)
            roleRegistry.removePermission(input.roleId, input.permissionName)
        },
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adminKeys.roles })
        },
    })
}
