import { createFileRoute, redirect } from '@tanstack/react-router'
import { LogOut, Mail, ShieldCheck, UserRound } from 'lucide-react'

import { AppShell } from '../components/app-shell'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card'
import { useAuth } from '../hooks/useAuth'

export const Route = createFileRoute('/settings')({
    component: SettingsComponent,
    beforeLoad: async () => {
        const token = localStorage.getItem('authToken')
        if (!token) {
            throw redirect({ to: '/login' })
        }
    },
})

function SettingsComponent() {
    const { user, logout } = useAuth()

    return (
        <AppShell
            active="settings"
            title="Settings"
            subtitle="Your profile and account security."
        >
            <div className="grid gap-6 lg:grid-cols-2">
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
