// ScrapGo.Core.Api client.
//
// Two responsibilities:
//   1. Authenticate against Google Cloud Identity Platform (GCIP) to obtain an
//      ID token. GCIP is the identity provider; the .NET API validates the
//      resulting RS256 ID token as a JWT bearer.
//   2. Call the ScrapGo.Core.Api with that token. The API is the only backend
//      the frontend talks to — it never talks to Quickbase directly.
//
// In development VITE_API_BASE_URL is empty and requests go to the Vite dev
// proxy (see vite.config.ts), which forwards /api to http://localhost:5141.

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''
const GCIP_API_KEY = import.meta.env.VITE_GCIP_API_KEY ?? ''

const GCIP_SIGN_IN_URL =
    'https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword'

/** The caller's own user record, as returned by GET /api/users/me. */
export interface CurrentUser {
    id: number
    identityPlatformUid: string
    email: string
    status: string
    classification: string
    workspaceSignInRequired: boolean
    roles: { roleId: number; name: string; organizationId: number | null; applicationId: number | null; expiresAt: string | null }[]
    // One entry per scope the caller holds anything in. organizationId null = platform scope.
    permissions: { organizationId: number | null; applicationId: number | null; permissions: string[] }[]
    organizations: {
        organizationId: number;
        name: string;
        permissions: string[];
        applications: {
            applicationId: number;
            key: string;
            name: string;
            modules: { moduleId: number; key: string; name: string }[];
            permissions: string[];
        }[];
    }[];
}

/** GCIP sign-in response (subset we care about). */
interface GcipSignInResponse {
    idToken: string
    refreshToken: string
    expiresIn: string
    localId: string
    registered: boolean
}

/** RFC 7807 ProblemDetails with the API's `reason` extension. */
interface ProblemDetails {
    title?: string
    status?: number
    detail?: string
    reason?: string
}

/** Error carrying the API's ProblemDetails `reason` for precise UI messaging. */
export class ApiError extends Error {
    readonly status: number
    readonly reason?: string

    constructor(message: string, status: number, reason?: string) {
        super(message)
        this.name = 'ApiError'
        this.status = status
        this.reason = reason
    }
}

/** Maps GCIP error codes to human-readable messages. */
function gcipErrorMessage(code: string | undefined): string {
    switch (code) {
        case 'EMAIL_NOT_FOUND':
        case 'INVALID_PASSWORD':
        case 'INVALID_LOGIN_CREDENTIALS':
            return 'Invalid email or password.'
        case 'USER_DISABLED':
            return 'This account has been disabled.'
        case 'TOO_MANY_ATTEMPTS_TRY_LATER':
            return 'Too many attempts. Please try again later.'
        case 'INVALID_EMAIL':
            return 'Please enter a valid email address.'
        default:
            return 'Unable to sign in. Please try again.'
    }
}

class ApiClient {
    private baseUrl: string
    private token: string | null = null

    constructor(baseUrl: string) {
        this.baseUrl = baseUrl
    }

    setAuthToken(token: string | null) {
        this.token = token
    }

    clearAuthToken() {
        this.token = null
    }

    /**
     * Authenticate with GCIP and return the ID token. The token is NOT stored
     * here — the caller (AuthProvider) owns persistence.
     */
    async signInWithPassword(email: string, password: string): Promise<string> {
        if (!GCIP_API_KEY) {
            throw new ApiError(
                'Authentication is not configured (missing VITE_GCIP_API_KEY).',
                0,
                'missing_gcip_api_key'
            )
        }

        const response = await fetch(`${GCIP_SIGN_IN_URL}?key=${GCIP_API_KEY}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password, returnSecureToken: true }),
        })

        const body = (await response.json()) as
            | GcipSignInResponse
            | { error?: { message?: string } }

        if (!response.ok || !('idToken' in body)) {
            const code = 'error' in body ? body.error?.message : undefined
            throw new ApiError(gcipErrorMessage(code), response.status, code)
        }

        return body.idToken
    }

    /**
     * Authenticate with Google Identity Services and exchange the ID token
     * for a GCIP ID token via accounts:signInWithIdp.
     */
    async signInWithGoogle(googleIdToken: string): Promise<string> {
        if (!GCIP_API_KEY) {
            throw new ApiError(
                'Authentication is not configured (missing VITE_GCIP_API_KEY).',
                0,
                'missing_gcip_api_key'
            )
        }

        // Exchange the Google ID token for a GCIP ID token via signInWithIdp
        const response = await fetch(
            `https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=${GCIP_API_KEY}`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    postBody: `id_token=${googleIdToken}&provider_id=google.com`,
                    requestUri: window.location.origin,
                    returnIdpCredential: true,
                    returnSecureToken: true,
                }),
            }
        )

        const body = (await response.json()) as
            | { idToken: string }
            | { error?: { message?: string } }

        if (!response.ok || !('idToken' in body)) {
            const code = 'error' in body ? body.error?.message : undefined
            throw new ApiError(gcipErrorMessage(code), response.status, code)
        }

        return body.idToken
    }

    /** Generic authenticated fetch against the ScrapGo.Core.Api. */
    async fetchWithAuth<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
        const response = await fetch(`${this.baseUrl}${endpoint}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...(this.token && { Authorization: `Bearer ${this.token}` }),
                ...options.headers,
            },
        })

        if (!response.ok) {
            let problem: ProblemDetails | undefined
            try {
                problem = (await response.json()) as ProblemDetails
            } catch {
                // Non-JSON error body — fall through to a generic message.
            }
            throw new ApiError(
                problem?.detail || problem?.title || `Request failed (${response.status})`,
                response.status,
                problem?.reason
            )
        }

        if (response.status === 204) {
            return undefined as T
        }

        return (await response.json()) as T
    }

    /**
     * GET /api/users/me — returns the caller's user record, auto-provisioning
     * it on first sight. This is the proof that the token is valid and the
     * backend recognises the caller.
     */
    async getCurrentUser(): Promise<CurrentUser> {
        return this.fetchWithAuth<CurrentUser>('/api/users/me')
    }
}

export const apiClient = new ApiClient(API_BASE_URL)