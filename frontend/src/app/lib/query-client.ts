import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './api-client'

/**
 * Shared TanStack Query client.
 *
 * Retries are disabled for 4xx responses (auth/permission/validation errors
 * are not transient), and enabled once for everything else.
 */
export const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: (failureCount, error) => {
                if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
                    return false
                }
                return failureCount < 1
            },
        },
        mutations: {
            retry: false,
        },
    },
})
