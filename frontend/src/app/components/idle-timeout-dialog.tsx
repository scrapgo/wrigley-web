import { useCallback } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Clock, LogOut } from 'lucide-react'

import {
    AlertDialog,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
} from './ui/alert-dialog'
import { Button } from './ui/button'
import { useAuth } from '../hooks/useAuth'
import { useIdleTimeout } from '../hooks/useIdleTimeout'
import { IDLE_TIMEOUT_MS } from '../lib/session-timeout'

/**
 * Signs the user out after IDLE_TIMEOUT_MS without activity, with a countdown
 * dialog first. Mounted once, inside AuthProvider; does nothing while signed out.
 */
export function IdleTimeout() {
    const { me, logout } = useAuth()
    const queryClient = useQueryClient()

    const signOut = useCallback(
        (reason?: 'idle') => {
            logout()
            // Nothing the user saw stays in memory after they're gone.
            queryClient.clear()
            window.location.assign(reason ? `/login?reason=${reason}` : '/login')
        },
        [logout, queryClient]
    )

    const { secondsLeft, stayActive } = useIdleTimeout(me !== null, () => signOut('idle'))

    if (secondsLeft === null) {
        return null
    }

    const minutes = Math.floor(secondsLeft / 60)
    const seconds = String(secondsLeft % 60).padStart(2, '0')

    return (
        <AlertDialog open>
            {/* No Escape or outside click: only the buttons decide. */}
            <AlertDialogContent onEscapeKeyDown={(e) => e.preventDefault()} className="max-w-[calc(100vw-2rem)] sm:max-w-md">
                <AlertDialogHeader>
                    <AlertDialogTitle className="flex items-center gap-2">
                        <Clock className="h-5 w-5 text-amber-600" />
                        Are you still there?
                    </AlertDialogTitle>
                    <AlertDialogDescription>
                        For security, you'll be signed out after {IDLE_TIMEOUT_MS / 60000} minutes of inactivity.
                    </AlertDialogDescription>
                </AlertDialogHeader>
                <p className="text-center text-4xl font-bold tabular-nums text-ink-900" role="timer" aria-live="polite">
                    {minutes}:{seconds}
                </p>
                <AlertDialogFooter>
                    <Button size="lg" variant="outline" className="w-full sm:w-auto" onClick={() => signOut()}>
                        <LogOut className="h-4 w-4" />
                        Sign out
                    </Button>
                    <Button size="lg" className="w-full sm:w-auto" onClick={stayActive} autoFocus>
                        Stay signed in
                    </Button>
                </AlertDialogFooter>
            </AlertDialogContent>
        </AlertDialog>
    )
}
