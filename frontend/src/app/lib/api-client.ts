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

const GCIP_SIGN_UP_URL = 'https://identitytoolkit.googleapis.com/v1/accounts:signUp'

const GCIP_SEND_OOB_CODE_URL = 'https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode'

const GCIP_UPDATE_URL = 'https://identitytoolkit.googleapis.com/v1/accounts:update'

/** Exchanges a GCIP refresh token for a new ID token. */
const GCIP_REFRESH_URL = 'https://securetoken.googleapis.com/v1/token'

/** Refresh this long before the ID token's expiry (GCIP ID tokens last one hour). */
export const REFRESH_MARGIN_MS = 5 * 60 * 1000

// localStorage keys. `authToken` is also read by the route guards.
const ID_TOKEN_KEY = 'authToken'
const REFRESH_TOKEN_KEY = 'authRefreshToken'
const EXPIRES_AT_KEY = 'authTokenExpiresAt'

/** A GCIP session: the ID token sent to the API and the refresh token that renews it. */
export interface AuthSession {
    idToken: string
    refreshToken: string
    /** Epoch milliseconds when the ID token expires. */
    expiresAt: number
}

function sessionFrom(idToken: string, refreshToken: string, expiresInSeconds: string | number): AuthSession {
    return { idToken, refreshToken, expiresAt: Date.now() + Number(expiresInSeconds) * 1000 }
}

/** The caller as GET /api/me returns them (ACCESS-MODEL.md section 9). */
export interface Me {
    user: {
        id: number
        identityPlatformUid: string
        email: string
        displayName: string | null
        /** `Internal` or `External`. */
        userType: string
        status: string
        lastSignInAt: string | null
    }
    /** Role keys, e.g. `scrapgo_user`. */
    roles: string[]
    /** Effective permission keys: granted by a role, and their feature is on. */
    permissions: string[]
    /** Keys of the features that are on for the caller. */
    features: string[]
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
        case 'OPERATION_NOT_ALLOWED':
            return 'Google sign-in is not enabled for this portal.'
        case 'INVALID_IDP_RESPONSE':
            return 'Google sign-in failed. Please try again.'
        case 'FEDERATED_USER_ID_ALREADY_LINKED':
        case 'NEED_CONFIRMATION':
            return 'An account with this email already uses a different sign-in method. Sign in with that method, or ask an administrator to link Google to your account.'
        case 'EMAIL_EXISTS':
            return 'An account with this email already exists. Sign in instead.'
        case 'WEAK_PASSWORD':
            return 'Choose a password of at least 6 characters.'
        case 'BLOCKING_FUNCTION_ERROR_RESPONSE':
            return 'Sign-in was blocked by the sign-in check. Please try again, or contact an administrator.'
        default:
            // Show unexpected codes (e.g. BLOCKING_FUNCTION_ERROR_RESPONSE) so failures are diagnosable.
            return code ? `Unable to sign in (${code}).` : 'Unable to sign in. Please try again.'
    }
}

type SessionListener = (session: AuthSession | null) => void

const ME_ENDPOINT = '/api/me'

/** sessionStorage key for an invitation token waiting to be sent to POST /api/session. */
export const PENDING_INVITATION_KEY = 'pendingInvitationToken'

/** Why POST /api/session or GET /api/me refused the caller (403 `reason`). */
export type AccessDenial = 'no_access' | 'account_disabled'

/** The denial behind an error, or null when the error is something else. */
export function accessDenialOf(error: unknown): AccessDenial | null {
    return error instanceof ApiError && error.status === 403 && (error.reason === 'no_access' || error.reason === 'account_disabled')
        ? error.reason
        : null
}

class ApiClient {
    private baseUrl: string
    private session: AuthSession | null = null
    private refreshing: Promise<AuthSession> | null = null
    private listeners = new Set<SessionListener>()
    private forbiddenListeners = new Set<() => void>()

    constructor(baseUrl: string) {
        this.baseUrl = baseUrl
    }

    /** The current session, if any. */
    getSession(): AuthSession | null {
        return this.session
    }

    /** Loads a session persisted by a previous visit; null when there is none. */
    restoreSession(): AuthSession | null {
        const idToken = localStorage.getItem(ID_TOKEN_KEY)
        if (!idToken) {
            return null
        }

        // A session from before refresh tokens were stored has no expiry: treat it as
        // expired, so the first request refreshes it (or the 401 signs the user out).
        this.session = {
            idToken,
            refreshToken: localStorage.getItem(REFRESH_TOKEN_KEY) ?? '',
            expiresAt: Number(localStorage.getItem(EXPIRES_AT_KEY) ?? 0),
        }
        return this.session
    }

    /** Uses the session for API calls without persisting it (until `persistSession`). */
    adoptSession(session: AuthSession) {
        this.session = session
        this.notify()
    }

    /** Stores the current session so it survives a reload. */
    persistSession() {
        if (!this.session) {
            return
        }

        localStorage.setItem(ID_TOKEN_KEY, this.session.idToken)
        localStorage.setItem(REFRESH_TOKEN_KEY, this.session.refreshToken)
        localStorage.setItem(EXPIRES_AT_KEY, String(this.session.expiresAt))
    }

    /** Forgets the session everywhere. */
    clearSession() {
        this.session = null
        this.refreshing = null
        localStorage.removeItem(ID_TOKEN_KEY)
        localStorage.removeItem(REFRESH_TOKEN_KEY)
        localStorage.removeItem(EXPIRES_AT_KEY)
        this.notify()
    }

    /** Called with the new session after each refresh, and with null when the session ends. */
    onSessionChange(listener: SessionListener): () => void {
        this.listeners.add(listener)
        return () => this.listeners.delete(listener)
    }

    /** Called after any 403 from an endpoint other than /api/me: the caller's access may have changed. */
    onForbidden(listener: () => void): () => void {
        this.forbiddenListeners.add(listener)
        return () => this.forbiddenListeners.delete(listener)
    }

    private notify() {
        for (const listener of this.listeners) {
            listener(this.session)
        }
    }

    /**
     * Renews the ID token with the refresh token. Concurrent callers share one
     * request. On failure the session is cleared (the user is signed out).
     */
    refreshSession(): Promise<AuthSession> {
        if (!this.refreshing) {
            this.refreshing = this.requestRefresh().finally(() => {
                this.refreshing = null
            })
        }
        return this.refreshing
    }

    private async requestRefresh(): Promise<AuthSession> {
        const refreshToken = this.session?.refreshToken
        if (!refreshToken || !GCIP_API_KEY) {
            this.clearSession()
            throw new ApiError('Your session has expired. Please sign in again.', 401, 'session_expired')
        }

        let response: Response
        try {
            response = await fetch(`${GCIP_REFRESH_URL}?key=${GCIP_API_KEY}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({ grant_type: 'refresh_token', refresh_token: refreshToken }).toString(),
            })
        } catch {
            // Offline or blocked: keep the session; the next request tries again.
            throw new ApiError('Could not reach the sign-in service. Check your connection.', 0, 'refresh_unreachable')
        }

        const body = (await response.json().catch(() => ({}))) as {
            id_token?: string
            refresh_token?: string
            expires_in?: string
        }

        if (!response.ok || !body.id_token) {
            this.clearSession()
            throw new ApiError('Your session has expired. Please sign in again.', 401, 'session_expired')
        }

        this.session = sessionFrom(body.id_token, body.refresh_token ?? refreshToken, body.expires_in ?? 3600)
        this.persistSession()
        this.notify()
        return this.session
    }

    /**
     * Authenticate with GCIP and return the session. It is NOT stored here —
     * the caller (AuthProvider) decides when to use and persist it.
     */
    async signInWithPassword(email: string, password: string): Promise<AuthSession> {
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

        return sessionFrom(body.idToken, body.refreshToken, body.expiresIn)
    }

    /**
     * Create an email/password account in GCIP (an invited external user). The
     * new account's email is unverified until they follow the verification link.
     */
    async signUp(email: string, password: string): Promise<AuthSession> {
        if (!GCIP_API_KEY) {
            throw new ApiError('Authentication is not configured (missing VITE_GCIP_API_KEY).', 0, 'missing_gcip_api_key')
        }

        const response = await fetch(`${GCIP_SIGN_UP_URL}?key=${GCIP_API_KEY}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password, returnSecureToken: true }),
        })

        const body = (await response.json()) as GcipSignInResponse | { error?: { message?: string } }
        if (!response.ok || !('idToken' in body)) {
            const code = 'error' in body ? body.error?.message?.split(' ')[0] : undefined
            throw new ApiError(gcipErrorMessage(code), response.status, code)
        }

        return sessionFrom(body.idToken, body.refreshToken, body.expiresIn)
    }

    /**
     * Sets the GCIP account's display name. Later ID tokens carry it as the
     * `name` claim, which POST /api/session stores on the user.
     */
    async setDisplayName(idToken: string, displayName: string): Promise<void> {
        const response = await fetch(`${GCIP_UPDATE_URL}?key=${GCIP_API_KEY}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ idToken, displayName, returnSecureToken: false }),
        })

        if (!response.ok) {
            const body = (await response.json().catch(() => ({}))) as { error?: { message?: string } }
            const code = body.error?.message
            throw new ApiError(code ? `Could not save your name (${code}).` : 'Could not save your name.', response.status, code)
        }
    }

    /**
     * Sends GCIP's email-verification link to the account behind `idToken`.
     * With `continueUrl`, GCIP's "email verified" page links on to it (its
     * origin must be an authorized domain in GCIP).
     */
    async sendEmailVerification(idToken: string, continueUrl?: string): Promise<void> {
        const response = await fetch(`${GCIP_SEND_OOB_CODE_URL}?key=${GCIP_API_KEY}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ requestType: 'VERIFY_EMAIL', idToken, ...(continueUrl && { continueUrl }) }),
        })

        if (!response.ok) {
            const body = (await response.json().catch(() => ({}))) as { error?: { message?: string } }
            const code = body.error?.message
            throw new ApiError(code ? `Could not send the verification email (${code}).` : 'Could not send the verification email.', response.status, code)
        }
    }

    /**
     * Authenticate with Google Identity Services and exchange the ID token
     * for a GCIP ID token via accounts:signInWithIdp.
     */
    async signInWithGoogle(googleIdToken: string): Promise<AuthSession> {
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
                    // GCIP expects the provider as `providerId` (not provider_id) in postBody.
                    postBody: new URLSearchParams({
                        id_token: googleIdToken,
                        providerId: 'google.com',
                    }).toString(),
                    requestUri: window.location.origin,
                    returnIdpCredential: true,
                    returnSecureToken: true,
                }),
            }
        )

        const body = (await response.json()) as {
            idToken?: string
            refreshToken?: string
            expiresIn?: string
            needConfirmation?: boolean
            error?: { message?: string }
        }

        if (!response.ok || !body.idToken) {
            // An existing account with this email and another sign-in method comes back
            // as 200 with needConfirmation and no idToken.
            const code = body.error?.message ?? (body.needConfirmation ? 'NEED_CONFIRMATION' : undefined)
            // Blocking-function rejections arrive as "BLOCKING_FUNCTION_ERROR_RESPONSE : ...".
            throw new ApiError(gcipErrorMessage(code?.split(' ')[0]), response.status, code)
        }

        return sessionFrom(body.idToken, body.refreshToken ?? '', body.expiresIn ?? 3600)
    }

    /**
     * Generic authenticated fetch against the ScrapGo.Core.Api. Refreshes the
     * ID token shortly before it expires, and on a 401 refreshes once and
     * retries the request once; if the refresh fails the user is signed out.
     */
    async fetchWithAuth<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
        if (this.session && this.session.expiresAt - Date.now() < REFRESH_MARGIN_MS) {
            await this.refreshSession()
        }

        let response = await this.send(endpoint, options)
        if (response.status === 401 && this.session) {
            await this.refreshSession()
            response = await this.send(endpoint, options)
        }

        if (response.status === 403 && endpoint !== ME_ENDPOINT) {
            for (const listener of this.forbiddenListeners) {
                listener()
            }
        }

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

    private send(endpoint: string, options: RequestInit): Promise<Response> {
        const token = this.session?.idToken
        return fetch(`${this.baseUrl}${endpoint}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...(token && { Authorization: `Bearer ${token}` }),
                ...options.headers,
            },
        })
    }

    /**
     * POST /api/session: the end of every sign-in. Creates the user when the
     * sign-in qualifies (a Workspace sign-in, or a valid invitation token).
     */
    async startSession(invitationToken: string | null): Promise<Me> {
        return this.fetchWithAuth<Me>('/api/session', {
            method: 'POST',
            body: JSON.stringify(invitationToken ? { invitationToken } : {}),
        })
    }

    /** GET /api/me: the caller's user, roles, effective permissions and features. */
    async getMe(): Promise<Me> {
        return this.fetchWithAuth<Me>(ME_ENDPOINT)
    }
}

export const apiClient = new ApiClient(API_BASE_URL)