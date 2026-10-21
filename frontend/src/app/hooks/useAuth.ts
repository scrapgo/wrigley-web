import React, { useState, useEffect, useContext, createContext, useCallback, useRef } from 'react'
import type { ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
    accessDenialOf,
    apiClient,
    PENDING_INVITATION_KEY,
    REFRESH_MARGIN_MS,
    type AuthSession,
    type Me,
} from '../lib/api-client'

/** React Query key for GET /api/me. */
export const meQueryKey = ['me'] as const

/** Access can change at any time (an admin edits roles or features), so re-read it this often. */
const ME_REFETCH_INTERVAL_MS = 5 * 60 * 1000

interface AuthContextType {
    /** The caller (GET /api/me), or null when signed out. */
    me: Me | null
    login: (email: string, password: string) => Promise<void>
    /** Sign in with a Google ID token from Google Identity Services. */
    loginWithGoogle: (googleIdToken: string) => Promise<void>
    logout: () => void
    /** Re-reads GET /api/me. */
    refreshMe: () => Promise<void>
    isAuthenticated: boolean
    /** True while the stored session is being validated against the API. */
    isLoading: boolean
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
    const queryClient = useQueryClient()
    const [hasSession, setHasSession] = useState(() => apiClient.restoreSession() !== null)
    const [expiresAt, setExpiresAt] = useState<number | null>(() => apiClient.getSession()?.expiresAt ?? null)
    // True between an explicit sign-out and the session-change notification it causes.
    const signingOut = useRef(false)

    const meQuery = useQuery({
        queryKey: meQueryKey,
        queryFn: () => apiClient.getMe(),
        enabled: hasSession,
        staleTime: 0,
        refetchOnWindowFocus: true,
        refetchInterval: ME_REFETCH_INTERVAL_MS,
    })
    const me = hasSession ? (meQuery.data ?? null) : null

    // Track the session: refreshes move the expiry; a cleared session means the
    // user is signed out. A refresh that failed (rather than a sign-out the user
    // asked for) sends them to the sign-in page.
    useEffect(
        () =>
            apiClient.onSessionChange((session) => {
                setExpiresAt(session?.expiresAt ?? null)
                if (session === null) {
                    setHasSession(false)
                    queryClient.removeQueries({ queryKey: meQueryKey })
                    if (!signingOut.current && window.location.pathname !== '/login') {
                        window.location.assign('/login')
                    }
                    signingOut.current = false
                }
            }),
        [queryClient]
    )

    // A 403 anywhere may mean the caller's access changed: re-read it.
    useEffect(
        () => apiClient.onForbidden(() => void queryClient.invalidateQueries({ queryKey: meQueryKey })),
        [queryClient]
    )

    // A session the API refuses (no user, or a disabled one) ends on the no-access
    // page. Any other failure on first load discards the stored session.
    useEffect(() => {
        if (!hasSession || !meQuery.isError) {
            return
        }

        const denial = accessDenialOf(meQuery.error)
        if (denial || !meQuery.data) {
            signingOut.current = true
            apiClient.clearSession()
        }
        if (denial && window.location.pathname !== '/no-access') {
            window.location.assign(`/no-access?reason=${denial}`)
        }
    }, [hasSession, meQuery.isError, meQuery.error, meQuery.data])

    // Refresh shortly before the ID token expires, so an idle tab stays signed in.
    useEffect(() => {
        if (expiresAt === null || me === null) {
            return
        }

        const delay = Math.max(expiresAt - REFRESH_MARGIN_MS - Date.now(), 0)
        const timer = window.setTimeout(() => {
            // A failed refresh clears the session itself; the listener above signs out.
            apiClient.refreshSession().catch(() => undefined)
        }, delay)

        return () => window.clearTimeout(timer)
    }, [expiresAt, me])

    // Every sign-in ends with POST /api/session (with a pending invitation token,
    // if any), then GET /api/me. Only then is the GCIP session persisted. A 403
    // no_access / account_disabled is rethrown for the caller to route on.
    const completeSignIn = useCallback(
        async (session: AuthSession) => {
            apiClient.adoptSession(session)
            const invitationToken = sessionStorage.getItem(PENDING_INVITATION_KEY)
            try {
                await apiClient.startSession(invitationToken)
                await queryClient.fetchQuery({ queryKey: meQueryKey, queryFn: () => apiClient.getMe(), staleTime: 0 })
                apiClient.persistSession()
                setHasSession(true)
            } catch (error) {
                signingOut.current = true
                apiClient.clearSession()
                throw error
            } finally {
                // One attempt per token: a refused invitation won't succeed on retry.
                if (invitationToken) {
                    sessionStorage.removeItem(PENDING_INVITATION_KEY)
                }
            }
        },
        [queryClient]
    )

    const login = useCallback(
        async (email: string, password: string) => {
            await completeSignIn(await apiClient.signInWithPassword(email, password))
        },
        [completeSignIn]
    )

    const loginWithGoogle = useCallback(
        async (googleIdToken: string) => {
            await completeSignIn(await apiClient.signInWithGoogle(googleIdToken))
        },
        [completeSignIn]
    )

    const refreshMe = useCallback(() => queryClient.invalidateQueries({ queryKey: meQueryKey }), [queryClient])

    const logout = useCallback(() => {
        signingOut.current = true
        apiClient.clearSession()
    }, [])

    const value: AuthContextType = {
        me,
        login,
        loginWithGoogle,
        logout,
        refreshMe,
        isAuthenticated: me !== null,
        isLoading: hasSession && meQuery.isPending,
    }

    return React.createElement(AuthContext.Provider, { value }, children)
}

export function useAuth() {
    const context = useContext(AuthContext)
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider')
    }
    return context
}
