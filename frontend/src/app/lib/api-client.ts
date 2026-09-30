// Base URL for the API - in development this would be http://localhost:5141
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5141'

interface LoginCredentials {
    email: string
    password: string
}

interface LoginResponse {
    idToken: string
    refreshToken: string
    expiresIn: string
    localId: string
    registered: boolean
}

interface User {
    id: number
    identityPlatformUid: string
    email: string
    status: string
    classification: string
}

class ApiClient {
    private baseUrl: string
    private token: string | null = null

    constructor(baseUrl: string) {
        this.baseUrl = baseUrl
    }

    // Set the authentication token
    setAuthToken(token: string) {
        this.token = token
    }

    // Clear the authentication token
    clearAuthToken() {
        this.token = null
    }

    // Generic fetch wrapper with auth header
    async fetchWithAuth(endpoint: string, options: RequestInit = {}) {
        const url = `${this.baseUrl}${endpoint}`

        const config: RequestInit = {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...(this.token && { 'Authorization': `Bearer ${this.token}` }),
                ...options.headers,
            },
        }

        const response = await fetch(url, config)

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`)
        }

        return response.json()
    }

    // Login endpoint (this would integrate with your backend auth system)
    async login(_credentials: LoginCredentials): Promise<LoginResponse> {
        // In a real implementation, this would call your backend auth endpoint
        // For now, we'll simulate a successful login

        // This is a mock response - in reality you'd get this from your backend
        return {
            idToken: 'mock-jwt-token',
            refreshToken: 'mock-refresh-token',
            expiresIn: '3600',
            localId: 'mock-user-id',
            registered: true
        }
    }

    // Get current user endpoint
    async getCurrentUser(): Promise<User> {
        // In a real implementation, this would call GET /api/users/me
        // For now, we'll return mock data

        return {
            id: 1,
            identityPlatformUid: 'mock-uid',
            email: 'user@example.com',
            status: 'Active',
            classification: 'Internal'
        }
    }

    // Example method for getting dashboard stats
    async getDashboardStats(): Promise<any> {
        // Mock data for dashboard stats
        return {
            activeLoads: 24,
            scrapIndexStatus: 'Healthy',
            pendingInquiries: 8,
            quickbaseSync: 'Up to date'
        }
    }
}

export const apiClient = new ApiClient(API_BASE_URL)