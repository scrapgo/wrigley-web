import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { LogOut, ShieldOff, UserX } from 'lucide-react'

import { BrandLogo } from '../components/brand-logo'
import { Button } from '../components/ui/button'
import { useAuth } from '../hooks/useAuth'
import type { AccessDenial } from '../lib/api-client'

export const Route = createFileRoute('/no-access')({
    component: NoAccessComponent,
    validateSearch: (search: Record<string, unknown>): { reason?: AccessDenial } => ({
        reason: search.reason === 'account_disabled' || search.reason === 'no_access' ? search.reason : undefined,
    }),
})

const MESSAGES: Record<AccessDenial, { title: string; body: string }> = {
    no_access: {
        title: 'Ask ScrapGo for an invitation',
        body: "This account doesn't have access to the ScrapGo portal yet. ScrapGo staff sign in with their Google Workspace account; everyone else joins by invitation.",
    },
    account_disabled: {
        title: 'Your account is disabled',
        body: 'Contact ScrapGo if you think this is a mistake.',
    },
}

function NoAccessComponent() {
    const { reason = 'no_access' } = Route.useSearch()
    const { logout } = useAuth()
    const navigate = useNavigate()
    const { title, body } = MESSAGES[reason]
    const Icon = reason === 'account_disabled' ? UserX : ShieldOff

    const signOut = () => {
        logout()
        navigate({ to: '/login' })
    }

    return (
        <div className="flex min-h-screen flex-col items-center justify-center bg-steel-50 px-6 py-10">
            <div className="w-full max-w-md animate-rise rounded-2xl border border-border bg-card p-8 text-center shadow-card">
                <div className="mb-6 flex justify-center">
                    <BrandLogo withTagline size={36} />
                </div>
                <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
                    <Icon className="h-6 w-6" />
                </span>
                <h1 className="mt-4 text-xl font-bold text-ink-900">{title}</h1>
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{body}</p>
                <Button size="lg" variant="outline" className="mt-6 w-full" onClick={signOut}>
                    <LogOut className="h-4 w-4" />
                    Sign out
                </Button>
            </div>
        </div>
    )
}
