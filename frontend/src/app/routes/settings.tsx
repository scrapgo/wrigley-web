import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createFileRoute, Link, redirect } from '@tanstack/react-router'
import { Link2, LogOut, Mail, MailCheck, ShieldCheck, UserRound } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '../components/ui/dialog'
import { useToast } from '../components/ui/toast'
import { useAuth } from '../hooks/useAuth'
import { apiClient } from '../lib/api-client'
import { linkProvider } from '../lib/admin-api'
import { adminErrorMessage } from '../lib/admin-errors'

export const Route = createFileRoute('/settings')({
    component: SettingsComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

const schema = z.object({
    password: z.string().min(1, 'Enter your password to re-authenticate.'),
})

type FormValues = z.infer<typeof schema>

function SettingsComponent() {
    const { user, logout } = useAuth()
    const { toast } = useToast()
    const [linkOpen, setLinkOpen] = useState(false)

    const {
        register,
        handleSubmit,
        reset,
        formState: { errors, isSubmitting },
    } = useForm<FormValues>({
        resolver: zodResolver(schema),
        defaultValues: { password: '' },
    })

    const onLink = handleSubmit(async (values) => {
        if (!user?.email) return
        try {
            // Obtain a fresh ID token by re-authenticating, then link it.
            const reauthIdToken = await apiClient.signInWithPassword(
                user.email,
                values.password
            )
            const result = await linkProvider(reauthIdToken)
            toast({
                title: 'Provider linked',
                description: result.provider
                    ? `Linked ${result.provider}.`
                    : 'Your sign-in provider was linked.',
                variant: 'success',
            })
            reset()
            setLinkOpen(false)
        } catch (err) {
            toast({
                title: 'Could not link provider',
                description: adminErrorMessage(err),
                variant: 'destructive',
            })
        }
    })

    return (
        <AppShell
            active="settings"
            title="Settings"
            subtitle="Your profile and account security."
        >
            <div className="grid gap-6 lg:grid-cols-2">
                <Card className="lg:col-span-2">
                    <CardContent className="flex flex-col gap-3 py-5 sm:flex-row sm:items-center sm:justify-between">
                        <span className="flex items-center gap-2 text-sm">
                            <MailCheck className="h-4 w-4 text-brand-600" />
                            Invited to an organization? Accept it with the token you were sent.
                        </span>
                        <Button variant="outline" asChild>
                            <Link to="/invitations" search={{}}>Accept an invitation</Link>
                        </Button>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-lg">
                            <UserRound className="h-5 w-5 text-brand-600" />
                            Profile
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <Field
                            icon={Mail}
                            label="Email"
                            value={user?.email || '—'}
                        />
                        <Field
                            icon={ShieldCheck}
                            label="Classification"
                            value={
                                <Badge
                                    variant={
                                        user?.classification === 'Internal'
                                            ? 'success'
                                            : 'neutral'
                                    }
                                >
                                    {user?.classification || 'Unknown'}
                                </Badge>
                            }
                        />
                        <Field
                            icon={UserRound}
                            label="Status"
                            value={
                                <Badge
                                    variant={user?.status === 'Active' ? 'success' : 'danger'}
                                >
                                    {user?.status || 'Unknown'}
                                </Badge>
                            }
                        />
                        <Field
                            icon={UserRound}
                            label="Identity UID"
                            value={
                                <span className="font-mono text-xs text-muted-foreground">
                                    {user?.identityPlatformUid || '—'}
                                </span>
                            }
                        />
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-lg">
                            <Link2 className="h-5 w-5 text-brand-600" />
                            Linked providers
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <p className="text-sm text-muted-foreground">
                            Link an additional sign-in provider to your account. You will be
                            asked to re-authenticate to confirm it is you.
                        </p>
                        <Button onClick={() => setLinkOpen(true)}>
                            <Link2 className="h-4 w-4" />
                            Link a provider
                        </Button>
                    </CardContent>
                </Card>

                <Card className="lg:col-span-2">
                    <CardHeader>
                        <CardTitle className="text-lg">Session</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <Button variant="outline" onClick={logout}>
                            <LogOut className="h-4 w-4" />
                            Sign out
                        </Button>
                    </CardContent>
                </Card>
            </div>

            <Dialog open={linkOpen} onOpenChange={setLinkOpen}>
                <DialogContent>
                    <form onSubmit={onLink}>
                        <DialogHeader>
                            <DialogTitle>Re-authenticate</DialogTitle>
                            <DialogDescription>
                                Enter your password to obtain a fresh token for linking.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-2 py-4">
                            <Label htmlFor="reauth-password">Password</Label>
                            <Input
                                id="reauth-password"
                                type="password"
                                autoComplete="current-password"
                                {...register('password')}
                            />
                            {errors.password && (
                                <p className="text-sm text-red-600">
                                    {errors.password.message}
                                </p>
                            )}
                        </div>
                        <DialogFooter>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => setLinkOpen(false)}
                            >
                                Cancel
                            </Button>
                            <Button type="submit" disabled={isSubmitting}>
                                {isSubmitting ? 'Linking…' : 'Link provider'}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>
        </AppShell>
    )
}

function Field({
    icon: Icon,
    label,
    value,
}: {
    icon: React.ComponentType<{ className?: string }>
    label: string
    value: React.ReactNode
}) {
    return (
        <div className="flex items-center justify-between gap-4 border-b border-border pb-3 last:border-0 last:pb-0">
            <span className="flex items-center gap-2 text-sm text-muted-foreground">
                <Icon className="h-4 w-4" />
                {label}
            </span>
            <span className="text-sm font-medium text-ink-900">{value}</span>
        </div>
    )
}
