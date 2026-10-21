import React, { useState, useEffect, useContext, createContext, useCallback, useRef } from 'react'
import type { ReactNode } from 'react'
import { apiClient, REFRESH_MARGIN_MS, type AuthSession, type CurrentUser } from '../lib/api-client'

interface AuthContextType {
    user: CurrentUser | null
    login: (email: string, password: string) => Promise<void>
    /** Sign in with a Google ID token from Google Identity Services. */
    loginWithGoogle: (googleIdToken: string) => Promise<void>
    logout: () => void
    /** Re-reads the caller's user record. */
    refreshUser: () => Promise<void>
    isAuthenticated: boolean
    /** True while the stored session is being validated against the API. */
    isLoading: boolean
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<CurrentUser | null>(null)
    // Only "loading" when there is a stored session to validate.
    const [isLoading, setIsLoading] = useState(() => apiClient.restoreSession() !== null)
    const [expiresAt, setExpiresAt] = useState<number | null>(() => apiClient.getSession()?.expiresAt ?? null)
    // True between an explicit sign-out and the session-change notification it causes.
    const signingOut = useRef(false)

    // Track the session: refreshes move the expiry; a cleared session means the
    // user is signed out. A refresh that failed (rather than a sign-out the user
    // asked for) sends them to the sign-in page.
    useEffect(
        () =>
            apiClient.onSessionChange((session) => {
                setExpiresAt(session?.expiresAt ?? null)
                if (session === null) {
                    setUser(null)
                    if (!signingOut.current && window.location.pathname !== '/login') {
                        window.location.assign('/login')
                    }
                    signingOut.current = false
                }
            }),
        []
    )

    // Refresh shortly before the ID token expires, so an idle tab stays signed in.
    useEffect(() => {
        if (expiresAt === null || user === null) {
            return
        }

        const delay = Math.max(expiresAt - REFRESH_MARGIN_MS - Date.now(), 0)
        const timer = window.setTimeout(() => {
            // A failed refresh clears the session itself; the listener above signs out.
            apiClient.refreshSession().catch(() => undefined)
        }, delay)

        return () => window.clearTimeout(timer)
    }, [expiresAt, user])

    // On mount, validate any stored session against the API (refreshing it if it
    // has expired). A session the backend rejects is discarded.
    useEffect(() => {
        if (!apiClient.getSession()) {
            return
        }

        let cancelled = false

        apiClient
            .getCurrentUser()
            .then((me) => {
                if (!cancelled) setUser(me)
            })
            .catch(() => {
                signingOut.current = true
                apiClient.clearSession()
                if (!cancelled) setUser(null)
            })
            .finally(() => {
                if (!cancelled) setIsLoading(false)
            })

        return () => {
            cancelled = true
        }
    }, [])

    // Prove a fresh GCIP session works against our API before persisting it.
    const completeSignIn = useCallback(async (session: AuthSession) => {
        apiClient.adoptSession(session)
        try {
            const me = await apiClient.getCurrentUser()
            apiClient.persistSession()
            setUser(me)
        } catch (error) {
            signingOut.current = true
            apiClient.clearSession()
            throw error
        }
    }, [])

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

    const refreshUser = useCallback(async () => {
        setUser(await apiClient.getCurrentUser())
    }, [])

    const logout = useCallback(() => {
        signingOut.current = true
        apiClient.clearSession()
        setUser(null)
    }, [])

    const value: AuthContextType = {
        user,
        login,
        loginWithGoogle,
        logout,
        refreshUser,
        isAuthenticated: user !== null,
        isLoading,
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
