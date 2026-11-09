/**
 * Shown when POST /api/session refuses an invitation. The API answers 403
 * no_access for every failed check (by design), so the message names each
 * likely cause.
 */
export const INVITATION_REFUSED =
    "We couldn't accept this invitation. It may have expired, already been used, or been sent to a different email address. " +
    'Sign in with the address the invitation was sent to, and make sure that email is verified. If it still fails, ask ScrapGo for a new invitation.'

/** Why asking for a new invitation link failed, from the API's `reason`. */
export function renewErrorMessage(reason: string | undefined) {
    switch (reason) {
        case 'renew_too_soon':
            return 'A new link was just sent. Check your inbox (and spam folder), or try again in a minute.'
        case 'renew_limit_reached':
            return "This invitation can't be renewed again. Ask your ScrapGo contact to resend it."
        case 'invitation_not_renewable':
            return 'This invitation can no longer be renewed. Try signing in, or ask ScrapGo for a new invitation.'
        default:
            return "We couldn't send a new link. Please try again, or ask ScrapGo to resend your invitation."
    }
}

/**
 * Where the email-verification link sends the invitee once their address is
 * verified: the sign-in page, carrying the invitation so signing in there (often
 * in a new tab, without this tab's sessionStorage) still accepts it.
 */
export function invitationContinueUrl(token: string) {
    return `${window.location.origin}/login?invitation=${encodeURIComponent(token)}`
}
