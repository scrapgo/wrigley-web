import React, { useState, useEffect, useContext, createContext } from 'react'
import type { ReactNode } from 'react'

interface User {
    id: number
    email: string
    name: string
}

interface AuthContextType {
    user: User | null
    login: (email: string, password: string) => Promise<void>
    logout: () => void
    isAuthenticated: boolean
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null)
    const [isAuthenticated, setIsAuthenticated] = useState(false)

    useEffect(() => {
        // Check if user is already logged in
        const token = localStorage.getItem('authToken')
        if (token) {
            // In a real app, you would validate the token with the backend
            setIsAuthenticated(true)
            // Set a mock user for demo purposes
            setUser({
                id: 1,
                email: 'user@example.com',
                name: 'Demo User'
            })
        }
    }, [])

    const login = async (email: string, _password: string) => {
        // In a real app, you would make an API call to authenticate the user
        // For demo purposes, we'll just simulate a successful login

        // Simulate API delay
        await new Promise(resolve => setTimeout(resolve, 1000))

        // Mock successful login
        const mockUser = {
            id: 1,
            email: email,
            name: 'Demo User'
        }

        // Store token (in a real app, this would be a JWT from the backend)
        localStorage.setItem('authToken', 'mock-jwt-token')

        setUser(mockUser)
        setIsAuthenticated(true)
    }

    const logout = () => {
        localStorage.removeItem('authToken')
        setUser(null)
        setIsAuthenticated(false)
    }

    const value = {
        user,
        login,
        logout,
        isAuthenticated
    }

    return React.createElement(
        AuthContext.Provider,
        { value: value },
        children
    )
}

export function useAuth() {
    const context = useContext(AuthContext)
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider')
    }
    return context
}