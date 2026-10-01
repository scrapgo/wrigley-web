import { ArrowUpRight, ArrowDownRight } from 'lucide-react'

import { Card, CardContent } from './ui/card'
import { Skeleton } from './ui/skeleton'
import { cn } from '../lib/utils'
export interface Stat {
    title: string
    value: string
    delta: string
    trend: 'up' | 'down'
    icon: React.ComponentType<{ className?: string }>
    tone: string
    /** Optional series rendered as a sparkline beneath the value. */
    spark?: number[]
}

export function StatCard({
    stat,
    loading,
    index,
}: {
    stat: Stat
    loading: boolean
    index: number
}) {
    const Icon = stat.icon
    const TrendIcon = stat.trend === 'up' ? ArrowUpRight : ArrowDownRight

    if (loading) {
        return (
            <Card>
                <CardContent className="p-5">
                    <div className="flex items-center justify-between">
                        <Skeleton className="h-4 w-24" />
                        <Skeleton className="h-9 w-9 rounded-lg" />
                    </div>
                    <Skeleton className="mt-4 h-8 w-20" />
                    <Skeleton className="mt-3 h-3 w-28" />
                </CardContent>
            </Card>
        )
    }

    return (
        <Card
            className="animate-rise transition-shadow hover:shadow-elevated"
            style={{ animationDelay: `${index * 60}ms` }}
        >
            <CardContent className="p-5">
                <div className="flex items-center justify-between">
                    <p className="text-sm font-medium text-muted-foreground">{stat.title}</p>
                    <span
                        className={cn(
                            'flex h-9 w-9 items-center justify-center rounded-lg',
                            stat.tone
                        )}
                    >
                        <Icon className="h-4 w-4" />
                    </span>
                </div>
                <p className="mt-3 text-2xl font-bold tracking-tight text-ink-900">
                    {stat.value}
                </p>
                <p
                    className={cn(
                        'mt-1.5 inline-flex items-center gap-1 text-xs font-semibold',
                        stat.trend === 'up' ? 'text-brand-600' : 'text-amber-600'
                    )}
                >
                    <TrendIcon className="h-3.5 w-3.5" />
                    {stat.delta}
                </p>
                {stat.spark && (
                    <div className="mt-3">
                        <Sparkline points={stat.spark} tone={stat.trend} />
                    </div>
                )}
            </CardContent>
        </Card>
    )
}

/** Lightweight SVG area chart — no chart dependency required. */
export function MarketChart() {
    const points = [38, 42, 36, 48, 44, 56, 52, 64, 60, 72, 68, 80]
    const w = 640
    const h = 180
    const max = Math.max(...points)
    const min = Math.min(...points)
    const stepX = w / (points.length - 1)
    const scaleY = (v: number) => h - ((v - min) / (max - min)) * (h - 24) - 12

    const line = points
        .map((p, i) => `${i === 0 ? 'M' : 'L'} ${i * stepX} ${scaleY(p)}`)
        .join(' ')
    const area = `${line} L ${w} ${h} L 0 ${h} Z`

    return (
        <div className="w-full">
            <svg
                viewBox={`0 0 ${w} ${h}`}
                className="h-48 w-full"
                preserveAspectRatio="none"
                role="img"
                aria-label="Scrap index trend"
            >
                <defs>
                    <linearGradient id="area-fill" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#38ad27" stopOpacity="0.28" />
                        <stop offset="100%" stopColor="#38ad27" stopOpacity="0" />
                    </linearGradient>
                </defs>
                {[0.25, 0.5, 0.75].map((g) => (
                    <line
                        key={g}
                        x1="0"
                        x2={w}
                        y1={h * g}
                        y2={h * g}
                        stroke="currentColor"
                        className="text-border"
                        strokeDasharray="4 6"
                        strokeWidth="1"
                    />
                ))}
                <path d={area} fill="url(#area-fill)" />
                <path
                    d={line}
                    fill="none"
                    stroke="#38ad27"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                />
            </svg>
            <div className="mt-3 flex items-center justify-between text-xs text-muted-foreground">
                <span>Jan</span>
                <span>Mar</span>
                <span>May</span>
                <span>Jul</span>
                <span>Sep</span>
                <span>Nov</span>
            </div>
        </div>
    )
}

/** Compact dependency-free sparkline for stat cards. */
export function Sparkline({
    points,
    tone = 'up',
    className,
}: {
    points: number[]
    tone?: 'up' | 'down'
    className?: string
}) {
    const w = 120
    const h = 36
    const max = Math.max(...points)
    const min = Math.min(...points)
    const span = max - min || 1
    const stepX = w / (points.length - 1)
    const scaleY = (v: number) => h - ((v - min) / span) * (h - 6) - 3
    const line = points
        .map((p, i) => `${i === 0 ? 'M' : 'L'} ${i * stepX} ${scaleY(p)}`)
        .join(' ')
    const stroke = tone === 'up' ? '#38ad27' : '#d97706'
    const gid = `spark-${tone}`

    return (
        <svg
            viewBox={`0 0 ${w} ${h}`}
            className={cn('h-9 w-full', className)}
            preserveAspectRatio="none"
            aria-hidden
        >
            <defs>
                <linearGradient id={gid} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={stroke} stopOpacity="0.24" />
                    <stop offset="100%" stopColor={stroke} stopOpacity="0" />
                </linearGradient>
            </defs>
            <path d={`${line} L ${w} ${h} L 0 ${h} Z`} fill={`url(#${gid})`} />
            <path
                d={line}
                fill="none"
                stroke={stroke}
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
            />
        </svg>
    )
}

/** Circular progress ring used for onboarding / completion meters. */
export function ProgressRing({
    value,
    size = 132,
    stroke = 12,
    label,
    sublabel,
}: {
    value: number
    size?: number
    stroke?: number
    label?: string
    sublabel?: string
}) {
    const radius = (size - stroke) / 2
    const circumference = 2 * Math.PI * radius
    const offset = circumference - (Math.min(Math.max(value, 0), 100) / 100) * circumference

    return (
        <div className="relative inline-flex items-center justify-center" style={{ width: size, height: size }}>
            <svg width={size} height={size} className="-rotate-90" aria-hidden>
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    strokeWidth={stroke}
                    className="stroke-muted"
                />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    strokeWidth={stroke}
                    strokeLinecap="round"
                    stroke="#38ad27"
                    strokeDasharray={circumference}
                    strokeDashoffset={offset}
                    style={{ transition: 'stroke-dashoffset 0.8s cubic-bezier(0.22, 1, 0.36, 1)' }}
                />
            </svg>
            <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="font-display text-2xl font-bold text-ink-900">
                    {label ?? `${value}%`}
                </span>
                {sublabel && (
                    <span className="mt-0.5 text-[0.6875rem] font-medium uppercase tracking-wide text-muted-foreground">
                        {sublabel}
                    </span>
                )}
            </div>
        </div>
    )
}

export interface TickerItem {
    symbol: string
    name: string
    price: string
    change: string
    trend: 'up' | 'down'
}

/** Infinite marquee strip for live commodity pricing. */
export function PriceTicker({ items }: { items: TickerItem[] }) {
    const loop = [...items, ...items]

    return (
        <div className="marquee-mask relative overflow-hidden">
            <div className="animate-marquee flex w-max items-center gap-8">
                {loop.map((item, i) => (
                    <div key={`${item.symbol}-${i}`} className="flex items-center gap-2.5 whitespace-nowrap">
                        <span className="text-xs font-bold text-ink-900">{item.symbol}</span>
                        <span className="text-xs text-muted-foreground">{item.name}</span>
                        <span className="text-xs font-semibold text-ink-800">{item.price}</span>
                        <span
                            className={cn(
                                'inline-flex items-center gap-0.5 text-xs font-semibold',
                                item.trend === 'up' ? 'text-brand-600' : 'text-amber-600'
                            )}
                        >
                            {item.trend === 'up' ? (
                                <ArrowUpRight className="h-3 w-3" />
                            ) : (
                                <ArrowDownRight className="h-3 w-3" />
                            )}
                            {item.change}
                        </span>
                        <span className="h-1 w-1 rounded-full bg-border" aria-hidden />
                    </div>
                ))}
            </div>
        </div>
    )
}
