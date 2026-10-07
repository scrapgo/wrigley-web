// Google Identity Services (GIS): renders the "Sign in with Google" button and
// hands back a Google ID token, which api-client exchanges with GCIP
// (accounts:signInWithIdp) for the GCIP ID token our API accepts.
//
// Platform administration needs this path: the GCIP beforeSignIn blocking
// function copies the Workspace domain (hd) from Google's token into the GCIP
// token, and the API checks it against INTERNAL_HD_ALLOWLIST on every request.
// Email/password sign-ins never carry hd.

const GIS_SCRIPT_URL = 'https://accounts.google.com/gsi/client'

/** OAuth web client id of the Google provider configured in GCIP. Empty hides the button. */
export const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? ''

/** Optional Workspace domain hint for the account chooser. Not a security control. */
const GOOGLE_HOSTED_DOMAIN_HINT = import.meta.env.VITE_GOOGLE_HOSTED_DOMAIN ?? ''

interface GisCredentialResponse {
    credential: string
}

interface GisApi {
    accounts: {
        id: {
            initialize(config: {
                client_id: string
                callback: (response: GisCredentialResponse) => void
                hd?: string
                ux_mode?: 'popup' | 'redirect'
                cancel_on_tap_outside?: boolean
            }): void
            renderButton(
                parent: HTMLElement,
                options: {
                    type?: 'standard' | 'icon'
                    theme?: 'outline' | 'filled_blue' | 'filled_black'
                    size?: 'large' | 'medium' | 'small'
                    text?: 'signin_with' | 'continue_with'
                    shape?: 'rectangular' | 'pill'
                    width?: number
                }
            ): void
        }
    }
}

declare global {
    interface Window {
        google?: GisApi
    }
}

let gisLoader: Promise<GisApi> | null = null

function loadGis(): Promise<GisApi> {
    if (window.google?.accounts?.id) {
        return Promise.resolve(window.google)
    }

    gisLoader ??= new Promise<GisApi>((resolve, reject) => {
        const script = document.createElement('script')
        script.src = GIS_SCRIPT_URL
        script.async = true
        script.defer = true
        script.onload = () =>
            window.google?.accounts?.id
                ? resolve(window.google)
                : reject(new Error('Google sign-in failed to load.'))
        script.onerror = () => {
            gisLoader = null
            reject(new Error('Google sign-in failed to load.'))
        }
        document.head.appendChild(script)
    })

    return gisLoader
}

/**
 * Renders the Google button into `parent`. `onCredential` receives the Google
 * ID token after the user picks an account.
 */
export async function renderGoogleSignInButton(
    parent: HTMLElement,
    onCredential: (googleIdToken: string) => void
): Promise<void> {
    const gis = await loadGis()

    gis.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: (response) => onCredential(response.credential),
        ux_mode: 'popup',
        cancel_on_tap_outside: true,
        ...(GOOGLE_HOSTED_DOMAIN_HINT && { hd: GOOGLE_HOSTED_DOMAIN_HINT }),
    })

    parent.replaceChildren()
    gis.accounts.id.renderButton(parent, {
        type: 'standard',
        theme: 'outline',
        size: 'large',
        text: 'signin_with',
        shape: 'rectangular',
        width: Math.min(parent.clientWidth || 384, 400),
    })
}
