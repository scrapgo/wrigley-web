import { useCallback, useEffect, useRef, useState } from 'react'

import {
    ACTIVITY_WRITE_INTERVAL_MS,
    IDLE_TIMEOUT_MS,
    IDLE_WARNING_MS,
    readLastActivity,
    recordActivity,
} from '../lib/session-timeout'

/** What the user does, as opposed to background refreshes and refetches. */
const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'wheel', 'touchstart', 'scroll'] as const

/**
 * Tracks the user's last activity (shared across tabs) while `enabled`.
 * Returns the seconds left while the countdown is showing, else null, and
 * `stayActive` for the dialog's "Stay signed in". Calls `onTimeout` once the
 * user has been idle for IDLE_TIMEOUT_MS.
 */
export function useIdleTimeout(enabled: boolean, onTimeout: () => void) {
    const [secondsLeft, setSecondsLeft] = useState<number | null>(null)
    const lastActivity = useRef(0)
    const warning = useRef(false)
    const onTimeoutRef = useRef(onTimeout)

    useEffect(() => {
        onTimeoutRef.current = onTimeout
    }, [onTimeout])

    const stayActive = useCallback(() => {
        const now = Date.now()
        lastActivity.current = now
        recordActivity(now)
        warning.current = false
        setSecondsLeft(null)
    }, [])

    useEffect(() => {
        if (!enabled) {
            return
        }

        // A fresh sign-in has no recorded activity; a reload keeps the old value,
        // so a session left idle while the tab was closed ends right away.
        lastActivity.current = readLastActivity() ?? Date.now()
        recordActivity(lastActivity.current)

        const onActivity = () => {
            // While the countdown shows, only its buttons count.
            if (warning.current) {
                return
            }
            const now = Date.now()
            if (now - lastActivity.current >= ACTIVITY_WRITE_INTERVAL_MS) {
                lastActivity.current = now
                recordActivity(now)
            }
        }

        // Timestamps, not counted ticks, so a sleeping laptop is measured correctly.
        const check = () => {
            const last = Math.max(lastActivity.current, readLastActivity() ?? 0)
            lastActivity.current = last
            const idle = Date.now() - last

            if (idle >= IDLE_TIMEOUT_MS) {
                warning.current = false
                setSecondsLeft(null)
                onTimeoutRef.current()
            } else if (idle >= IDLE_TIMEOUT_MS - IDLE_WARNING_MS) {
                warning.current = true
                setSecondsLeft(Math.ceil((IDLE_TIMEOUT_MS - idle) / 1000))
            } else if (warning.current) {
                // Another tab's "Stay signed in" (or activity there) closes this tab's countdown too.
                warning.current = false
                setSecondsLeft(null)
            }
        }

        for (const name of ACTIVITY_EVENTS) {
            window.addEventListener(name, onActivity, { passive: true, capture: true })
        }
        const timer = window.setInterval(check, 1000)
        check()

        return () => {
            for (const name of ACTIVITY_EVENTS) {
                window.removeEventListener(name, onActivity, { capture: true })
            }
            window.clearInterval(timer)
            warning.current = false
        }
    }, [enabled])

    // A countdown left over from before a sign-out never shows.
    return { secondsLeft: enabled ? secondsLeft : null, stayActive }
}
