import { ApiError } from './api-client'

/**
 * Maps the backend's ProblemDetails `reason` codes to friendly messages.
 * Falls back to the server's detail/title, then a generic message.
 */
const REASON_MESSAGES: Record<string, string> = {
    // Organizations
    invalid_name: 'Enter a name with at least one letter or digit.',
    duplicate_slug: 'An organization with this name already exists.',
    // Roles
    invalid_request: 'Please check the form and try again.',
    duplicate_role_name: 'A role with this name already exists in this organization.',
    role_still_assigned: 'This role is still assigned to users and cannot be deleted.',
    unknown_permission: 'That permission is not in the catalog.',
    not_organization_administrator:
        'You must be an organization administrator to do that.',
    // Auth / linking
    invalid_reauth_token: 'The re-authentication token is invalid or expired.',
    reauth_identity_mismatch: 'That account does not match your signed-in identity.',
    reauth_not_fresh: 'Please re-authenticate and try again immediately.',
    user_not_provisioned: 'Your user record was not found. Sign out and back in.',
    // Generic
    organization_context_required: 'An organization context is required.',
    no_active_membership: 'You are not an active member of that organization.',
    user_disabled: 'Your account is disabled.',
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
