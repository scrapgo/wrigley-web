import { useState } from 'react'
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { MailCheck } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import { useToast } from '../components/ui/toast'
import { useAuth } from '../hooks/useAuth'
import { acceptInvitation } from '../lib/admin-api'
import { adminErrorMessage } from '../lib/admin-errors'

// `?token=...` pre-fills the form, so an invitation can be sent as a link.
export const Route = createFileRoute('/invitations')({
    component: InvitationsComponent,
    validateSearch: (search: Record<string, unknown>): { token?: string } => ({
        token: typeof search.token === 'string' ? search.token : undefined,
    }),
    beforeLoad: async () => {
        if (!localStorage.getItem('authToken')) {
            throw redirect({ to: '/login' })
        }
    },
})

function InvitationsComponent() {
    const { token: initialToken } = Route.useSearch()
    const [token, setToken] = useState(initialToken ?? '')
    const [accepting, setAccepting] = useState(false)
    const { user, refreshUser } = useAuth()
    const { toast } = useToast()
    const navigate = useNavigate()

    const accept = async () => {
        if (!token.trim()) {
            toast({ title: 'Paste the invitation token first', variant: 'destructive' })
            return
        }
        setAccepting(true)
        try {
            const result = await acceptInvitation(token.trim())
            await refreshUser()
            toast({
                title: 'Invitation accepted',
                description: `You joined the organization with ${result.grantedCount} role${result.grantedCount === 1 ? '' : 's'}.`,
                variant: 'success',
            })
            navigate({ to: '/admin' })
        } catch (err) {
            toast({ title: 'Could not accept the invitation', description: adminErrorMessage(err), variant: 'destructive' })
        } finally {
            setAccepting(false)
        }
    }

    return (
        <AppShell active="settings" title="Accept an invitation" subtitle="Join an organization you were invited to.">
            <Card className="max-w-xl">
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <MailCheck className="h-5 w-5" />
                        Invitation token
                    </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                    <p className="text-sm text-muted-foreground">
                        Paste the token you were sent. You must be signed in as the invited email
                        address{user ? ` (you are ${user.email})` : ''}, with a verified email; signing in
                        with Google verifies it.
                    </p>
                    <div className="grid gap-2">
                        <Label htmlFor="invitation-token">Token</Label>
                        <Input
                            id="invitation-token"
                            autoComplete="off"
                            value={token}
                            onChange={(e) => setToken(e.target.value)}
                            placeholder="Paste the token"
                        />
                    </div>
                    <Button size="lg" onClick={() => void accept()} disabled={accepting}>
                        {accepting ? 'Accepting…' : 'Accept invitation'}
                    </Button>
                </CardContent>
            </Card>
        </AppShell>
    )
}
