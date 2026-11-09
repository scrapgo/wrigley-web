import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Loader2, MailCheck } from 'lucide-react'

import { BrandLogo } from '../components/brand-logo'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { useAuth } from '../hooks/useAuth'
import { ApiError, apiClient, type MfaCodeSent } from '../lib/api-client'

/** Email MFA: an External user's sign-in waits here for the 6-digit code. */
export const Route = createFileRoute('/verify')({
    component: VerifyComponent,
})

/** Messages for the API's MFA `reason` codes. */
function mfaErrorMessage(error: unknown): string {
    switch (error instanceof ApiError ? error.reason : undefined) {
        case 'mfa_invalid_code':
            return "That code isn't right. Check the email and try again."
        case 'mfa_locked':
            return 'Too many wrong codes. Send a new code and try again.'
        case 'mfa_code_expired':
            return 'That code has expired. Send a new one.'
        case 'mfa_no_code':
            return 'Send a code first.'
        case 'mfa_resend_too_soon':
            return 'A code was just sent. Wait a minute before asking for another.'
        case 'mfa_send_limit_reached':
        case 'mfa_invalid_session':
            return 'Too many codes for this sign-in. Use a different account, then sign in again.'
        case 'mfa_email_unavailable':
            return "We couldn't email your code. Try again in a few minutes."
        default:
            return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
    }
}

function VerifyComponent() {
    const { mfaPending, completeMfa, cancelMfa } = useAuth()
    const navigate = useNavigate()
    const [code, setCode] = useState('')
    const [sent, setSent] = useState<MfaCodeSent | null>(null)
    const [now, setNow] = useState(() => Date.now())
    const autoSent = useRef(false)

    const send = useMutation({ mutationFn: () => apiClient.sendMfaCode(), onSuccess: setSent })
    const verify = useMutation({
        mutationFn: async () => {
            await apiClient.verifyMfaCode(code)
            await completeMfa()
        },
        // Only External users are asked for a code; they land on their suppliers.
        onSuccess: () => navigate({ to: '/suppliers' }),
    })

    // Only a sign-in waiting for its code belongs here.
    useEffect(() => {
        if (!mfaPending) {
            navigate({ to: '/login' })
        }
    }, [mfaPending, navigate])

    // Send the first code on arrival, once.
    useEffect(() => {
        if (mfaPending && !autoSent.current) {
            autoSent.current = true
            send.mutate()
        }
    }, [mfaPending, send])

    // Ticks the "Resend in 0:42" countdown.
    useEffect(() => {
        const timer = window.setInterval(() => setNow(Date.now()), 1000)
        return () => window.clearInterval(timer)
    }, [])

    const resendIn = sent ? Math.max(0, Math.ceil((Date.parse(sent.resendAvailableAt) - now) / 1000)) : 0

    const onSubmit = (e: FormEvent) => {
        e.preventDefault()
        verify.mutate()
    }

    const switchAccount = () => {
        cancelMfa()
        navigate({ to: '/login' })
    }

    const error = verify.error ?? send.error

    return (
        <div className="flex min-h-screen flex-col items-center justify-center bg-steel-50 px-4 py-10">
            <div className="w-full max-w-md animate-rise rounded-2xl border border-border bg-card p-6 text-center shadow-card sm:p-8">
                <div className="mb-6 flex justify-center">
                    <BrandLogo withTagline size={36} />
                </div>
                <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
                    <MailCheck className="h-6 w-6" />
                </span>
                <h1 className="mt-4 text-xl font-bold text-ink-900">Check your email</h1>
                <p className="mt-2 text-sm text-muted-foreground">
                    {sent ? (
                        <>
                            We emailed a 6-digit sign-in code to <span className="font-semibold text-ink-900">{sent.emailHint}</span>. It
                            expires in 10 minutes.
                        </>
                    ) : send.isPending ? (
                        'Sending your sign-in code…'
                    ) : (
                        'Enter the 6-digit code we email you to finish signing in.'
                    )}
                </p>

                {error && (
                    <div role="alert" className="mt-5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-left text-sm font-medium text-red-700">
                        {mfaErrorMessage(error)}
                    </div>
                )}

                <form onSubmit={onSubmit} className="mt-6 space-y-4 text-left">
                    <div className="space-y-2">
                        <Label htmlFor="mfa-code">Sign-in code</Label>
                        <Input
                            id="mfa-code"
                            inputMode="numeric"
                            autoComplete="one-time-code"
                            pattern="[0-9 ]{6,7}"
                            maxLength={7}
                            required
                            autoFocus
                            placeholder="123456"
                            className="h-12 text-center text-2xl tracking-[0.4em] tabular-nums"
                            value={code}
                            onChange={(e) => setCode(e.target.value.replace(/[^0-9 ]/g, ''))}
                        />
                    </div>
                    <Button type="submit" size="lg" className="w-full" disabled={verify.isPending || code.replace(/\D/g, '').length !== 6}>
                        {verify.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                        Verify and continue
                    </Button>
                </form>

                <div className="mt-4 flex flex-col gap-2">
                    <Button
                        type="button"
                        size="lg"
                        variant="outline"
                        className="w-full"
                        disabled={send.isPending || resendIn > 0}
                        onClick={() => send.mutate()}
                    >
                        {send.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                        {resendIn > 0 ? `Resend code in 0:${String(resendIn).padStart(2, '0')}` : 'Resend code'}
                    </Button>
                    <button
                        type="button"
                        className="min-h-11 text-sm font-semibold text-brand-600 hover:text-brand-700"
                        onClick={switchAccount}
                    >
                        Use a different account
                    </button>
                </div>
            </div>
        </div>
    )
}
