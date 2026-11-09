import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Clock, KeyRound, Loader2, MailCheck } from 'lucide-react'

import { BrandLogo } from '../components/brand-logo'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { useAuth } from '../hooks/useAuth'
import { accessDenialOf, ApiError, apiClient, PENDING_INVITATION_KEY } from '../lib/api-client'
import { GOOGLE_CLIENT_ID, renderGoogleSignInButton } from '../lib/google-identity'
import { INVITATION_REFUSED, invitationContinueUrl, renewErrorMessage } from '../lib/invitation'

export const Route = createFileRoute('/invite/accept')({
    component: AcceptInvitationComponent,
    validateSearch: (search: Record<string, unknown>): { token?: string } => ({
        token: typeof search.token === 'string' && search.token ? search.token : undefined,
    }),
})

type Step = 'choose' | 'create' | 'verify' | 'signin'

function AcceptInvitationComponent() {
    const { token } = Route.useSearch()

    // Check the link before anyone creates an account: an expired one can be renewed instead.
    const lookup = useQuery({
        queryKey: ['invitation-lookup', token],
        queryFn: () => apiClient.lookupInvitation(token!),
        enabled: !!token,
        retry: false,
        staleTime: Infinity,
    })

    if (!token) {
        return (
            <Frame>
                <h1 className="text-xl font-bold text-ink-900">This invitation link is incomplete</h1>
                <p className="mt-2 text-sm text-muted-foreground">
                    Open the full link from your invitation, or ask ScrapGo to send a new one.
                </p>
            </Frame>
        )
    }

    if (lookup.isPending) {
        return (
            <Frame>
                <Loader2 className="mx-auto h-6 w-6 animate-spin text-muted-foreground" aria-label="Checking your invitation" />
            </Frame>
        )
    }

    // If the check itself failed, fall through to the sign-in: POST /api/session still checks the token.
    switch (lookup.data?.status) {
        case 'Expired':
            return <ExpiredInvitation token={token} emailHint={lookup.data.emailHint} />
        case 'Accepted':
            return (
                <ClosedInvitation title="This invitation was already used">
                    Your account is set up. Sign in to continue.
                </ClosedInvitation>
            )
        case 'Revoked':
            return (
                <ClosedInvitation title="This invitation was withdrawn">
                    Ask ScrapGo to send you a new one if you still need access.
                </ClosedInvitation>
            )
        case 'NotFound':
            return (
                <ClosedInvitation title="This invitation link isn't valid">
                    A newer link may have replaced it. Use the most recent invitation email, or ask ScrapGo for a new one.
                </ClosedInvitation>
            )
        default:
            return <OpenInvitation token={token} />
    }
}

/** An expired invitation: the invitee can have a new link emailed to the invited address. */
function ExpiredInvitation({ token, emailHint }: { token: string; emailHint: string | null }) {
    const renew = useMutation({ mutationFn: () => apiClient.renewInvitation(token) })

    if (renew.isSuccess) {
        return (
            <Frame>
                <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
                    <MailCheck className="h-6 w-6" />
                </span>
                <h1 className="mt-4 text-xl font-bold text-ink-900">Check your inbox</h1>
                <p className="mt-2 text-sm text-muted-foreground">
                    We sent a new invitation link to <span className="font-semibold text-ink-900">{emailHint ?? 'your email'}</span>.
                    It expires in 15 minutes. This link no longer works.
                </p>
            </Frame>
        )
    }

    return (
        <Frame>
            <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-amber-50 text-amber-600">
                <Clock className="h-6 w-6" />
            </span>
            <h1 className="mt-4 text-xl font-bold text-ink-900">This invitation has expired</h1>
            <p className="mt-2 text-sm text-muted-foreground">
                Invitation links work for 15 minutes. We can email a new one to{' '}
                <span className="font-semibold text-ink-900">{emailHint ?? 'the invited address'}</span>.
            </p>
            {renew.isError && (
                <div role="alert" className="mt-5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-left text-sm font-medium text-red-700">
                    {renewErrorMessage(renew.error instanceof ApiError ? renew.error.reason : undefined)}
                </div>
            )}
            <Button size="lg" className="mt-6 w-full" disabled={renew.isPending} onClick={() => renew.mutate()}>
                {renew.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <MailCheck className="h-4 w-4" />}
                Email me a new link
            </Button>
        </Frame>
    )
}

function ClosedInvitation({ title, children }: { title: string; children: React.ReactNode }) {
    return (
        <Frame>
            <h1 className="text-xl font-bold text-ink-900">{title}</h1>
            <p className="mt-2 text-sm text-muted-foreground">{children}</p>
            <Button asChild size="lg" variant="outline" className="mt-6 w-full">
                <a href="/login">Go to sign in</a>
            </Button>
        </Frame>
    )
}

/** A pending invitation: create an account or sign in, which accepts it. */
function OpenInvitation({ token }: { token: string }) {
    const { login, loginWithGoogle } = useAuth()
    const navigate = useNavigate()
    const googleButtonRef = useRef<HTMLDivElement>(null)

    const [step, setStep] = useState<Step>('choose')
    const [displayName, setDisplayName] = useState('')
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const [error, setError] = useState('')
    const [busy, setBusy] = useState(false)

    // Keep the token for the sign-in that follows; POST /api/session sends it.
    // Stored again before every attempt, since an attempt consumes it.
    const storeToken = useCallback(() => {
        sessionStorage.setItem(PENDING_INVITATION_KEY, token)
    }, [token])

    useEffect(storeToken, [storeToken])

    /** Signs in (which sends the invitation), then opens the suppliers the invitation linked. */
    const accept = useCallback(
        async (signIn: () => Promise<void>) => {
            setBusy(true)
            setError('')
            storeToken()
            try {
                await signIn()
                navigate({ to: '/suppliers' })
            } catch (err) {
                const denial = accessDenialOf(err)
                setError(
                    denial === 'account_disabled'
                        ? 'Your account is disabled. Contact ScrapGo.'
                        : denial === 'no_access'
                          ? INVITATION_REFUSED
                          : err instanceof Error
                            ? err.message
                            : 'Unable to sign in. Please try again.'
                )
            } finally {
                setBusy(false)
            }
        },
        [navigate, storeToken]
    )

    useEffect(() => {
        const container = googleButtonRef.current
        if (step !== 'choose' || !GOOGLE_CLIENT_ID || !container) {
            return
        }

        renderGoogleSignInButton(
            container,
            (googleIdToken) => void accept(() => loginWithGoogle(googleIdToken)),
            'continue_with'
        ).catch((err: unknown) => setError(err instanceof Error ? err.message : 'Google sign-in failed to load.'))
    }, [step, accept, loginWithGoogle])

    // A new password account starts unverified: send the link, then wait for the user.
    const createAccount = async (e: FormEvent) => {
        e.preventDefault()
        setBusy(true)
        setError('')
        try {
            const session = await apiClient.signUp(email, password)
            // On the GCIP account, so the token after verification carries it as `name`.
            await apiClient.setDisplayName(session.idToken, displayName.trim())
            // The link in that email ends on the sign-in page, carrying the invitation.
            await apiClient.sendEmailVerification(session.idToken, invitationContinueUrl(token))
            setStep('verify')
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Could not create the account.')
        } finally {
            setBusy(false)
        }
    }

    const signIn = (e: FormEvent) => {
        e.preventDefault()
        void accept(() => login(email, password))
    }

    return (
        <Frame>
            <h1 className="text-xl font-bold text-ink-900">You're invited to ScrapGo</h1>
            <p className="mt-1.5 text-sm text-muted-foreground">
                Use the Google account or email address the invitation was sent to.
            </p>

            {error && (
                <div role="alert" className="mt-5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-left text-sm font-medium text-red-700">
                    {error}
                </div>
            )}

            {step === 'choose' && (
                <div className="mt-6 space-y-3">
                    {/* Google first: no password to choose and no verification email. */}
                    {GOOGLE_CLIENT_ID && (
                        <>
                            <div ref={googleButtonRef} className="flex min-h-11 justify-center" aria-busy={busy} />
                            <p className="pt-2 text-xs text-muted-foreground">or</p>
                        </>
                    )}
                    <Button
                        size="lg"
                        variant={GOOGLE_CLIENT_ID ? 'outline' : 'default'}
                        className="w-full"
                        onClick={() => setStep('create')}
                        disabled={busy}
                    >
                        <KeyRound className="h-4 w-4" />
                        Create a password
                    </Button>
                    <button
                        type="button"
                        className="pt-2 text-sm font-semibold text-brand-600 hover:text-brand-700"
                        onClick={() => setStep('signin')}
                    >
                        Already have a password? Sign in
                    </button>
                </div>
            )}

            {(step === 'create' || step === 'signin') && (
                <form onSubmit={step === 'create' ? createAccount : signIn} className="mt-6 space-y-4 text-left">
                    {step === 'create' && (
                        <div className="space-y-2">
                            <Label htmlFor="invite-name">Your name</Label>
                            <Input
                                id="invite-name"
                                autoComplete="name"
                                required
                                maxLength={100}
                                value={displayName}
                                onChange={(e) => setDisplayName(e.target.value)}
                            />
                        </div>
                    )}
                    <div className="space-y-2">
                        <Label htmlFor="invite-email">Email address</Label>
                        <Input id="invite-email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
                    </div>
                    <div className="space-y-2">
                        <Label htmlFor="invite-password">{step === 'create' ? 'Choose a password' : 'Password'}</Label>
                        <Input
                            id="invite-password"
                            type="password"
                            autoComplete={step === 'create' ? 'new-password' : 'current-password'}
                            required
                            minLength={step === 'create' ? 6 : undefined}
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                        />
                    </div>
                    <Button type="submit" size="lg" className="w-full" disabled={busy}>
                        {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                        {step === 'create' ? 'Create account' : 'Sign in and accept'}
                    </Button>
                    <button
                        type="button"
                        className="w-full text-center text-sm font-semibold text-brand-600 hover:text-brand-700"
                        onClick={() => {
                            setError('')
                            setStep('choose')
                        }}
                    >
                        Back
                    </button>
                </form>
            )}

            {step === 'verify' && (
                <div className="mt-6 space-y-4">
                    <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
                        <MailCheck className="h-6 w-6" />
                    </span>
                    <p className="text-sm text-muted-foreground">
                        We sent a verification link to <span className="font-semibold text-ink-900">{email}</span>. Open it, then
                        continue to sign in: signing in finishes accepting your invitation. If you verified in this browser, you can
                        also continue here.
                    </p>
                    <Button size="lg" className="w-full" disabled={busy} onClick={() => void accept(() => login(email, password))}>
                        {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                        I've verified my email — continue
                    </Button>
                </div>
            )}
        </Frame>
    )
}

function Frame({ children }: { children: React.ReactNode }) {
    return (
        <div className="flex min-h-screen flex-col items-center justify-center bg-steel-50 px-6 py-10">
            <div className="w-full max-w-md animate-rise rounded-2xl border border-border bg-card p-8 text-center shadow-card">
                <div className="mb-6 flex justify-center">
                    <BrandLogo withTagline size={36} />
                </div>
                {children}
            </div>
        </div>
    )
}
