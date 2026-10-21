/** Native <select> styled like the Input primitive (touch-friendly height). */
export const SELECT_CLASS =
    'h-11 rounded-lg border border-input bg-background px-3 text-sm outline-none transition-colors focus:border-brand-500 focus:ring-2 focus:ring-ring/25'

export function formatDateTime(value: string | null | undefined) {
    return value ? new Date(value).toLocaleString() : 'Never'
}
