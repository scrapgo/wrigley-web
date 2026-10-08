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
    // Auth / linking
    invalid_reauth_token: 'The re-authentication token is invalid or expired.',
    reauth_identity_mismatch: 'That account does not match your signed-in identity.',
    reauth_not_fresh: 'Please re-authenticate and try again immediately.',
    user_not_provisioned: 'Your user record was not found. Sign out and back in.',
    // Generic
    organization_context_required: 'An organization context is required.',
    no_active_membership: 'You are not an active member of that organization.',
    user_disabled: 'Your account is disabled.',
    // New error reasons from backend
    missing_permission: 'You don\'t have permission to do that in this organization.',
    cannot_modify_own_role: 'You can\'t change a role you hold yourself. Ask another administrator.',
    cannot_grant_unheld_permission: 'You can only grant permissions you hold yourself.',
    platform_admin_required: 'Only a platform administrator can change platform roles.',
    cannot_disable_self: 'You can\'t disable your own account.',
    role_not_found: 'That role isn\'t available in this organization.',
    role_scope_mismatch: 'That role can\'t be assigned in this scope.',
    user_not_a_member: 'Add the user to the organization first.',
    last_platform_administrator: 'Assign another platform administrator first.',
    last_organization_administrator: 'Assign another organization administrator first.',
    invitation_not_found: 'This invitation is no longer valid.',
    invitation_expired: 'This invitation has expired. Ask for a new one.',
    email_not_verified: 'Verify your email address (or sign in with Google), then accept again.',
    invitation_email_mismatch: 'Sign in with the email address the invitation was sent to.',
    invitation_pending: 'This email already has a pending invitation.',
    application_not_assigned: 'Assign the application to the organization first.',
    application_not_found: "This organization doesn't have that application.",
    module_not_found: "That module isn't part of this application.",
    last_application_administrator: 'Appoint another application administrator first.',
    organization_deactivated: 'This organization has been deactivated.',
    external_user_not_allowed: "External users can't hold platform roles.",
    organization_active: 'Deactivate the organization before deleting it.',
    user_not_found: 'No user with that id or email. They must sign in once first.',
    workspace_sign_in_required: 'Platform administration requires signing in with your Google Workspace account. Sign out and use "Sign in with Google".',
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
