import { sanitizeHtml } from '../../lib/supplier-format'

/**
 * Renders a Quickbase field value.
 *
 * `richText` should be `true` only for fields Quickbase types as `rich-text`
 * (see `RICH_TEXT_FIELD_IDS` in `supplier-api`) — those store HTML that
 * Quickbase renders in the cell (e.g. a colored status chip or a "Delete"
 * button). Rich text is sanitized with DOMPurify before rendering; everything
 * else is shown as plain, escaped text. Unsanitized HTML is never passed to
 * the DOM.
 */
export function RichText({
    value,
    richText = false,
    className,
    empty = '—',
}: {
    value: string | null | undefined
    richText?: boolean
    className?: string
    empty?: string
}) {
    if (value == null || value === '') {
        return <span className={className}>{empty}</span>
    }

    if (richText) {
        const clean = sanitizeHtml(value)
        return clean ? (
            <span
                className={className}
                dangerouslySetInnerHTML={{ __html: clean }}
            />
        ) : (
            <span className={className}>{empty}</span>
        )
    }

    return <span className={className}>{value}</span>
}
