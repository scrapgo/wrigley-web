import { Badge } from '../ui/badge'
import type { DataFreshness } from '../../lib/supplier-api'

const LABELS: Record<string, string> = {
    Cache: 'Cached',
    Quickbase: 'Live',
    StaleCache: 'Stale',
}

const VARIANTS: Record<string, 'success' | 'neutral' | 'warning'> = {
    Cache: 'neutral',
    Quickbase: 'success',
    StaleCache: 'warning',
}

/**
 * Shows where supplier data came from and when it was fetched. `StaleCache`
 * means Quickbase was unreachable and an older copy was served.
 */
export function FreshnessBadge({ freshness }: { freshness: DataFreshness }) {
    const label = LABELS[freshness.source] ?? freshness.source
    const variant = VARIANTS[freshness.source] ?? 'neutral'
    const fetched = new Date(freshness.fetchedAt)

    return (
        <Badge variant={variant} title={`Fetched ${fetched.toLocaleString()}`}>
            {label}
        </Badge>
    )
}
