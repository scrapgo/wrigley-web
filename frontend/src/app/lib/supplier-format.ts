// Number/currency/date formatting shared by the supplier views.

import DOMPurify from 'dompurify'

export function formatNumber(value: number | null | undefined): string {
    return value == null ? '—' : value.toLocaleString()
}

export function formatCurrency(value: number | null | undefined): string {
    return value == null
        ? '—'
        : value.toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

export function formatDate(value: string | null | undefined): string | null {
    if (!value) return null
    const date = new Date(value)
    return isNaN(date.getTime()) ? value : date.toLocaleDateString()
}

/**
 * True when a Quickbase value is rich HTML rather than plain text. These
 * fields are meant to be rendered (e.g. a styled "Delete" button in the
 * Prospect Status cell), so the portal renders them instead of showing tags.
 */
export function isHtml(value: string | null | undefined): value is string {
    return typeof value === 'string' && /<[a-z][\s\S]*>/i.test(value)
}

/**
 * Reduces a value that may contain rich-text HTML to plain text. Use this
 * where markup can't be rendered (badges, `tel:`/`mailto:` links, aria labels).
 * Returns null for empty results.
 */
export function htmlToText(value: string | null | undefined): string | null {
    if (value == null) return null
    if (!isHtml(value)) return value || null

    if (typeof document === 'undefined') {
        const text = value.replace(/<[^>]*>/g, ' ').replace(/&nbsp;/gi, ' ').trim()
        return text || null
    }

    const div = document.createElement('div')
    div.innerHTML = value
    const text = (div.textContent ?? '').replace(/\u00a0/g, ' ').trim()
    return text || null
}

/** The CSS properties Quickbase's rich-text toolbar emits inline. */
const ALLOWED_STYLE_PROPS = [
    'text-align',
    'background',
    'background-color',
    'color',
    'text-shadow',
    'text-decoration',
    'padding',
    'padding-top',
    'padding-right',
    'padding-bottom',
    'padding-left',
    'display',
    'width',
    'height',
    'border',
    'border-radius',
    'border-color',
    'border-width',
    'border-style',
    'font-weight',
    'font-style',
    'font-size',
    'margin',
    'margin-top',
    'margin-right',
    'margin-bottom',
    'margin-left',
]

/**
 * Sanitizes Quickbase rich-text HTML for safe rendering with
 * `dangerouslySetInnerHTML`. Strips scripts, event handlers and other unsafe
 * markup (DOMPurify), and trims inline styles to the presentation properties
 * the Quickbase editor emits, so a stored payload can't hijack the layout.
 */
export function sanitizeHtml(html: string): string {
    const clean = DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['a', 'b', 'strong', 'i', 'em', 'u', 's', 'br', 'p', 'div', 'span', 'small', 'font'],
        ALLOWED_ATTR: ['style', 'href', 'title', 'target', 'rel', 'color', 'size', 'face'],
        FORBID_TAGS: ['script', 'style', 'iframe', 'object', 'embed', 'form', 'input', 'button', 'svg'],
        FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
        ALLOW_DATA_ATTR: false,
    })

    // Server / no DOM: DOMPurify has already stripped the unsafe parts.
    if (typeof document === 'undefined') return clean

    // Keep only presentation-only inline styles (drop anything else).
    const template = document.createElement('template')
    template.innerHTML = clean
    template.content.querySelectorAll('[style]').forEach((element) => {
        const el = element as HTMLElement
        const kept: string[] = []
        for (let i = 0; i < el.style.length; i += 1) {
            const prop = el.style.item(i)
            if (ALLOWED_STYLE_PROPS.includes(prop)) {
                kept.push(`${prop}: ${el.style.getPropertyValue(prop)}`)
            }
        }
        if (kept.length > 0) {
            el.setAttribute('style', kept.join('; '))
        } else {
            el.removeAttribute('style')
        }
    })
    return template.innerHTML
}
