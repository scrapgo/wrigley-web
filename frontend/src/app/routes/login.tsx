import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useState, useEffect } from 'react'
import {
    ArrowRight,
    ShieldCheck,
    Truck,
    TrendingUp,
    Loader2,
    Target,
    MessageSquare,
} from 'lucide-react'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Label } from '../components/ui/label'
import { BrandLogo } from '../components/brand-logo'
import { useAuth } from '../hooks/useAuth'

export const Route = createFileRoute('/login')({
    component: LoginComponent,
})

const HIGHLIGHTS = [
    {
        icon: TrendingUp,
        title: 'Real-time & forecast pricing',
        body: 'The ScrapGo Index turns millions of transactions into pricing you can act on.',
    },
    {
        icon: Target,
        title: 'Qualified opportunities',
        body: 'Buyers see vetted scrap deals the moment our team qualifies them.',
    },
    {
        icon: Truck,
        title: 'Freight, handled',
        body: 'We dispatch the trucks — you focus on the material.',
    },
    {
        icon: MessageSquare,
        title: 'One proactive channel',
        body: 'Automated updates and direct contact, without the phone tag.',
    },
]

function LoginComponent() {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const [error, setError] = useState('')
    const [loading, setLoading] = useState(false)
    const { login, isAuthenticated } = useAuth()
    const navigate = useNavigate()

    useEffect(() => {
        if (isAuthenticated) {
            navigate({ to: '/dashboard' })
        }
    }, [isAuthenticated, navigate])

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault()
        setLoading(true)
        setError('')

        try {
            await login(email, password)
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Unable to sign in. Please try again.')
        } finally {
            setLoading(false)
        }
    }

    return (
        <div className="grid min-h-screen lg:grid-cols-2">
            {/* Brand panel */}
            <div className="relative hidden overflow-hidden ink-gradient lg:flex lg:flex-col lg:justify-between lg:p-12">
                <div className="absolute inset-0 grid-pattern opacity-40" aria-hidden />
                <div
                    className="absolute -right-24 -top-24 h-96 w-96 rounded-full bg-brand-500/25 blur-3xl"
                    aria-hidden
                />
                <div
                    className="absolute -bottom-32 -left-16 h-80 w-80 rounded-full bg-brand-600/20 blur-3xl"
                    aria-hidden
                />

                <div className="relative">
                    <BrandLogo inverted withTagline size={40} />
                </div>

                <div className="relative max-w-md animate-rise">
                    <p className="mb-4 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-semibold text-brand-300">
                        <span className="h-1.5 w-1.5 rounded-full bg-brand-400" />
                        The ScrapGo Stakeholder Portal
                    </p>
                    <h1 className="text-4xl font-bold leading-tight text-white">
                        One portal.
                        <br />
                        Every stakeholder.
                        <br />
                        <span className="gradient-text">Zero communication gaps.</span>
                    </h1>
                    <p className="mt-5 text-base leading-relaxed text-steel-300">
                        Downstream unifies pricing, opportunities, and freight into a single,
                        proactive line of communication between ScrapGo and the people who move
                        scrap.
                    </p>

                    <ul className="mt-8 space-y-4">
                        {HIGHLIGHTS.map(({ icon: Icon, title, body }) => (
                            <li key={title} className="flex items-start gap-3">
                                <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-500/15 text-brand-400">
                                    <Icon className="h-4 w-4" />
                                </span>
                                <span>
                                    <span className="block text-sm font-semibold text-white">
                                        {title}
                                    </span>
                                    <span className="block text-sm text-steel-400">{body}</span>
                                </span>
                            </li>
                        ))}
                    </ul>
                </div>

                <div className="relative flex items-center gap-6 text-xs text-steel-400">
                    <span className="inline-flex items-center gap-1.5">
                        <ShieldCheck className="h-3.5 w-3.5 text-brand-400" />
                        Secured by ScrapGo Identity
                    </span>
                    <span>© {new Date().getFullYear()} ScrapGo, LLC</span>
                    <a href="https://scrapgo.com/privacy-policy" className="hover:text-steel-200">
                        Privacy
                    </a>
                    <a href="https://scrapgo.com/contact-us/" className="hover:text-steel-200">
                        Contact
                    </a>
                </div>
            </div>

            {/* Form panel */}
            <div className="flex flex-col justify-center bg-background px-6 py-10 sm:px-10 lg:px-16">
                <div className="mx-auto w-full max-w-sm animate-rise">
                    <div className="mb-8 lg:hidden">
                        <BrandLogo withTagline size={38} />
                    </div>

                    <h2 className="text-2xl font-bold text-ink-900">Welcome back</h2>
                    <p className="mt-1.5 text-sm text-muted-foreground">
                        Sign in to the ScrapGo Downstream portal to continue.
                    </p>

                    {error && (
                        <div
                            role="alert"
                            className="mt-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700"
                        >
                            {error}
                        </div>
                    )}

                    <form onSubmit={handleSubmit} className="mt-6 space-y-5">
                        <div className="space-y-2">
                            <Label htmlFor="email">Email address</Label>
                            <Input
                                id="email"
                                type="email"
                                autoComplete="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                required
                                placeholder="you@scrapgo.com"
                            />
                        </div>

                        <div className="space-y-2">
                            <div className="flex items-center justify-between">
                                <Label htmlFor="password">Password</Label>
                                <a
                                    href="#"
                                    className="text-xs font-semibold text-brand-600 hover:text-brand-700"
                                >
                                    Forgot password?
                                </a>
                            </div>
                            <Input
                                id="password"
                                type="password"
                                autoComplete="current-password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                placeholder="••••••••"
                            />
                        </div>

                        <Button type="submit" size="lg" disabled={loading} className="w-full">
                            {loading ? (
                                <>
                                    <Loader2 className="h-4 w-4 animate-spin" />
                                    Signing in…
                                </>
                            ) : (
                                <>
                                    Sign in
                                    <ArrowRight className="h-4 w-4" />
                                </>
                            )}
                        </Button>
                    </form>

                    <p className="mt-8 text-center text-sm text-muted-foreground">
                        New to ScrapGo?{' '}
                        <a
                            href="https://scrapgo.com/register"
                            className="font-semibold text-brand-600 hover:text-brand-700"
                        >
                            Create an account
                        </a>
                    </p>
                </div>
            </div>
        </div>
    )
}
