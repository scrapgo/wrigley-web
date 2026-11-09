// Idle sign-out: a signed-in user who does nothing for IDLE_TIMEOUT_MS is
// signed out, after a countdown in the last IDLE_WARNING_MS. Background work
// (token refreshes, GET /api/me refetches) is not activity.

/** Signed out after this long without activity. */
export const IDLE_TIMEOUT_MS = 5 * 60 * 1000

/** The countdown dialog opens this long before the sign-out. */
export const IDLE_WARNING_MS = 60 * 1000

/** Activity is written at most this often, so scrolling doesn't hammer localStorage. */
export const ACTIVITY_WRITE_INTERVAL_MS = 5 * 1000

/**
 * localStorage key for the last activity (epoch ms). Shared by every tab, so
 * activity in one keeps them all signed in, and a stale value after a reload
 * (the laptop slept, the tab was closed) signs out at once.
 */
export const LAST_ACTIVITY_KEY = 'idleLastActivityAt'

/** The last activity in any tab, or null when none is recorded (a fresh sign-in). */
export function readLastActivity(): number | null {
    try {
        const value = Number(localStorage.getItem(LAST_ACTIVITY_KEY))
        return Number.isFinite(value) && value > 0 ? value : null
    } catch {
        return null
    }
}

export function recordActivity(at = Date.now()) {
    try {
        localStorage.setItem(LAST_ACTIVITY_KEY, String(at))
    } catch {
        // Storage blocked: this tab still tracks activity in memory.
    }
}

export function forgetActivity() {
    try {
        localStorage.removeItem(LAST_ACTIVITY_KEY)
    } catch {
        // Nothing stored.
    }
}
