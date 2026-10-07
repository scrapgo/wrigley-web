import React, { useState, useEffect, useContext, createContext, useCallback } from 'react'
import type { ReactNode } from 'react'
import { apiClient, type CurrentUser } from '../lib/api-client'

const TOKEN_KEY = 'authToken'

interface AuthContextType {
    user: CurrentUser | null
    login: (email: string, password: string) => Promise<void>
    /** Sign in with a Google ID token from Google Identity Services. */
    loginWithGoogle: (googleIdToken: string) => Promise<void>
    logout: () => void
    /** Re-reads GET /api/users/me, e.g. after joining an organization. */
    refreshUser: () => Promise<void>
    isAuthenticated: boolean
    /** True while the stored token is being validated against the API. */
    isLoading: boolean
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<CurrentUser | null>(null)
    // Only "loading" when there is a stored token to validate.
    const [isLoading, setIsLoading] = useState(() => localStorage.getItem(TOKEN_KEY) !== null)

    // On mount, validate any stored token against GET /api/users/me. A token
    // that the backend rejects (expired, disabled user) is discarded.
    useEffect(() => {
        const token = localStorage.getItem(TOKEN_KEY)
        if (!token) {
            return
        }

        apiClient.setAuthToken(token)
        let cancelled = false

        apiClient
            .getCurrentUser()
            .then((me) => {
                if (!cancelled) setUser(me)
            })
            .catch(() => {
                apiClient.clearAuthToken()
                localStorage.removeItem(TOKEN_KEY)
                if (!cancelled) setUser(null)
            })
            .finally(() => {
                if (!cancelled) setIsLoading(false)
            })

        return () => {
            cancelled = true
        }
    }, [])

    // Prove a fresh GCIP ID token works against our API before persisting it.
    const completeSignIn = useCallback(async (token: string) => {
        apiClient.setAuthToken(token)
        try {
            const me = await apiClient.getCurrentUser()
            localStorage.setItem(TOKEN_KEY, token)
            setUser(me)
        } catch (error) {
            apiClient.clearAuthToken()
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
        apiClient.clearAuthToken()
        localStorage.removeItem(TOKEN_KEY)
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