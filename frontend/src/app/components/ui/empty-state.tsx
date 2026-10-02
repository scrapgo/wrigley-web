import type { ReactNode } from "react"
import type { LucideIcon } from "lucide-react"

import { cn } from "../../lib/utils"

interface EmptyStateProps {
    icon?: LucideIcon
    title: string
    description?: string
    action?: ReactNode
    className?: string
}

export function EmptyState({
    icon: Icon,
    title,
    description,
    action,
    className,
}: EmptyStateProps) {
    return (
        <div
            className={cn(
                "flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-card/50 px-6 py-14 text-center",
                className
            )}
        >
            {Icon && (
                <span className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
                    <Icon className="h-6 w-6" />
                </span>
            )}
            <p className="text-base font-semibold text-ink-900">{title}</p>
            {description && (
                <p className="mt-1 max-w-sm text-sm text-muted-foreground">{description}</p>
            )}
            {action && <div className="mt-5">{action}</div>}
        </div>
    )
}
