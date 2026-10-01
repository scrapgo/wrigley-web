import React, { useState, useEffect, useContext, createContext, useCallback } from 'react'
import type { ReactNode } from 'react'
import { apiClient, type CurrentUser } from '../lib/api-client'

const TOKEN_KEY = 'authToken'

interface AuthContextType {
    user: CurrentUser | null
    login: (email: string, password: string) => Promise<void>
    logout: () => void
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

    const login = useCallback(async (email: string, password: string) => {
        // 1. Exchange credentials for a GCIP ID token.
        const token = await apiClient.signInWithPassword(email, password)
        apiClient.setAuthToken(token)

        // 2. Prove the token works against our API before persisting it.
        try {
            const me = await apiClient.getCurrentUser()
            localStorage.setItem(TOKEN_KEY, token)
            setUser(me)
        } catch (error) {
            apiClient.clearAuthToken()
            throw error
        }
    }, [])

    const logout = useCallback(() => {
        apiClient.clearAuthToken()
        localStorage.removeItem(TOKEN_KEY)
        setUser(null)
    }, [])

    const value: AuthContextType = {
        user,
        login,
        logout,
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