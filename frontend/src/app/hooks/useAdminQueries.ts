import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
    disableUser,
    enableUser,
    getUser,
    listPermissions,
    listRoles,
    listUsers,
    setUserRoles,
    setUserSuppliers,
    type UserDetail,
    type UserListQuery,
} from '../lib/admin-api'

/* -------------------------------------------------------------------------- */
/*  Query keys                                                                 */
/* -------------------------------------------------------------------------- */

export const adminKeys = {
    permissions: ['admin', 'permissions'] as const,
    users: ['admin', 'users'] as const,
    userList: (query: UserListQuery) => ['admin', 'users', query] as const,
    userDetail: (id: number) => ['admin', 'user', id] as const,
    roles: ['admin', 'roles'] as const,
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

export function useUsers(query: UserListQuery) {
    return useQuery({
        queryKey: adminKeys.userList(query),
        queryFn: () => listUsers(query),
        placeholderData: keepPreviousData,
    })
}

export function useUserDetail(id: number) {
    return useQuery({
        queryKey: adminKeys.userDetail(id),
        queryFn: () => getUser(id),
    })
}

export function useRoles(enabled = true) {
    return useQuery({
        queryKey: adminKeys.roles,
        queryFn: listRoles,
        enabled,
    })
}

/* -------------------------------------------------------------------------- */
/*  Mutations                                                                  */
/* -------------------------------------------------------------------------- */

/** After a change to one user: refresh the list and that user's detail. */
function useInvalidateUser() {
    const queryClient = useQueryClient()
    return (id: number, detail?: UserDetail) => {
        if (detail) {
            queryClient.setQueryData(adminKeys.userDetail(id), detail)
        } else {
            void queryClient.invalidateQueries({ queryKey: adminKeys.userDetail(id) })
        }
        void queryClient.invalidateQueries({ queryKey: adminKeys.users })
    }
}

export function useDisableUser() {
    const invalidate = useInvalidateUser()
    return useMutation({
        mutationFn: (id: number) => disableUser(id),
        onSuccess: (_, id) => invalidate(id),
    })
}

export function useEnableUser() {
    const invalidate = useInvalidateUser()
    return useMutation({
        mutationFn: (id: number) => enableUser(id),
        onSuccess: (_, id) => invalidate(id),
    })
}

export function useSetUserRoles() {
    const invalidate = useInvalidateUser()
    return useMutation({
        mutationFn: ({ id, roleIds }: { id: number; roleIds: number[] }) => setUserRoles(id, roleIds),
        onSuccess: (detail, { id }) => invalidate(id, detail),
    })
}

export function useSetUserSuppliers() {
    const invalidate = useInvalidateUser()
    return useMutation({
        mutationFn: ({ id, supplierRecordIds }: { id: number; supplierRecordIds: number[] }) =>
            setUserSuppliers(id, supplierRecordIds),
        onSuccess: (detail, { id }) => invalidate(id, detail),
    })
}
