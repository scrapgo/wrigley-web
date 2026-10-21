import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { disableUser, enableUser, getUser, listPermissions, listUsers } from '../lib/admin-api'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const adminKeys = {
    permissions: ['admin', 'permissions'] as const,
    users: ['admin', 'users'] as const,
    userDetail: (id: number) => ['admin', 'user', id] as const,
}

/* -------------------------------------------------------------------------- */
/*  Queries                                                                    */
/* -------------------------------------------------------------------------- */

export function usePermissions() {
    return useQuery({
        queryKey: adminKeys.permissions,
        queryFn: listPermissions,
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
