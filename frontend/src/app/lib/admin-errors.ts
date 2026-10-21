import { ApiError } from './api-client'

/**
 * Maps the backend's ProblemDetails `reason` codes to friendly messages.
 * Falls back to the server's detail/title, then a generic message.
 */
const REASON_MESSAGES: Record<string, string> = {
    // General
    invalid_request: 'Please check the form and try again.',
    missing_permission: "You don't have permission to do that.",
    session_expired: 'Your session has expired. Please sign in again.',
    no_access: 'This account has no access to ScrapGo. Ask ScrapGo for an invitation.',
    account_disabled: 'This account is disabled.',
    mfa_required: 'Sign in again with your second factor.',
    // Users
    user_not_found: 'One of those users no longer exists.',
    cannot_disable_self: "You can't disable your own account. Ask another administrator.",
    last_site_admin: 'This would leave no active Site Admin. Assign or enable another one first.',
    role_not_found: "One of those roles doesn't exist.",
    role_type_mismatch: 'Internal users hold internal roles, and external users external roles.',
    supplier_links_external_only: 'Supplier links are for external users; internal users see every supplier.',
    // Invitations
    role_not_external: 'Invitations are for external users: choose an external role.',
    internal_email_not_invitable: "ScrapGo staff don't need an invitation: they sign in with their Google Workspace account.",
    invitation_pending: 'This email already has a pending invitation. Resend or revoke it.',
    invitation_not_pending: 'That invitation was already accepted or revoked.',
    // Roles
    audience_mismatch: 'Internal permissions go on internal roles, and external permissions on external roles.',
    unknown_permission: "That permission isn't in the catalog.",
    // Features
    feature_locked: 'This feature is locked: it is always on.',
    // Suppliers
    supplier_not_found: 'No supplier with that record id.',
    quickbase_unavailable: "Quickbase didn't answer and no cached copy was available. Try again shortly.",
}

export function adminErrorMessage(error: unknown): string {
    if (error instanceof ApiError) {
        if (error.reason && REASON_MESSAGES[error.reason]) {
            return REASON_MESSAGES[error.reason]
        }
        return error.message
    }
    if (error instanceof Error) {
        return error.message
    }
    return 'Something went wrong. Please try again.'
}
