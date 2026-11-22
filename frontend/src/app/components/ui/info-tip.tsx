import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Info } from 'lucide-react'

import { cn } from '../../lib/utils'
import { SUPPLIER_FIELD_TOOLTIPS, type SupplierTooltipKey } from '../../lib/supplier-tooltips'

type Side = 'top' | 'bottom'
type Align = 'start' | 'center' | 'end'

/**
 * A small "info" bubble that explains a pricing / dispatch field on hover or
 * keyboard focus. The text comes from `SUPPLIER_FIELD_TOOLTIPS` (by `field`)
 * or an explicit `text`, so wording is changed in one place without touching
 * the components that render it.
 *
 * The bubble is portalled to `document.body` and positioned against the
 * viewport, so it is never clipped by scrollable tables or cards. Meant for
 * desktop hover; on touch the button can be focused to reveal it.
 */
export function InfoTip({
    field,
    text,
    side = 'top',
    align = 'center',
    className,
}: {
    field?: SupplierTooltipKey
    text?: string
    side?: Side
    align?: Align
    className?: string
}) {
    const content = text ?? (field ? SUPPLIER_FIELD_TOOLTIPS[field] : '') ?? ''
    const anchorRef = useRef<HTMLButtonElement>(null)
    const [pos, setPos] = useState<{ top: number; left: number } | null>(null)

    const show = useCallback(() => {
        const rect = anchorRef.current?.getBoundingClientRect()
        if (!rect) return
        const left =
            align === 'start'
                ? rect.left
                : align === 'end'
                  ? rect.right
                  : rect.left + rect.width / 2
        const top = side === 'top' ? rect.top - 6 : rect.bottom + 6
        setPos({ top, left })
    }, [align, side])

    const hide = useCallback(() => setPos(null), [])

    // A open bubble should follow the anchor; close it on scroll/resize rather
    // than leaving it floating in the wrong place.
    useEffect(() => {
        if (!pos) return
        const close = () => setPos(null)
        window.addEventListener('scroll', close, true)
        window.addEventListener('resize', close)
        return () => {
            window.removeEventListener('scroll', close, true)
            window.removeEventListener('resize', close)
        }
    }, [pos])

    return (
        <span className={cn('relative inline-flex align-middle', className)}>
            <button
                ref={anchorRef}
                type="button"
                aria-label={content ? `More info: ${content}` : 'More info'}
                onMouseEnter={show}
                onMouseLeave={hide}
                onFocus={show}
                onBlur={hide}
                className="inline-flex h-4 w-4 items-center justify-center rounded-full text-muted-foreground/60 transition-colors hover:text-brand-600 focus:outline-none focus-visible:text-brand-600 focus-visible:ring-2 focus-visible:ring-brand-400"
            >
                <Info className="h-3.5 w-3.5" aria-hidden />
            </button>
            {pos && typeof document !== 'undefined'
                ? createPortal(
                      <span
                          role="tooltip"
                          style={{ top: pos.top, left: pos.left }}
                          className={cn(
                              'pointer-events-none fixed z-[100] w-max max-w-[16rem] whitespace-normal rounded-lg bg-ink-900 px-2.5 py-1.5 text-xs font-normal leading-snug text-white shadow-elevated',
                              align === 'start' && '',
                              align === 'center' && '-translate-x-1/2',
                              align === 'end' && '-translate-x-full',
                              side === 'top' && '-translate-y-full',
                              side === 'bottom' && ''
                          )}
                      >
                          {content || '\u00A0'}
                      </span>,
                      document.body
                  )
                : null}
        </span>
    )
}