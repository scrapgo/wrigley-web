# Admin Frontend Gaps v2: organizations, applications, modules

What the portal must change for the organization → application → module
access model now in the backend (see
[`../ORG-APP-MODULE-MODEL.md`](../ORG-APP-MODULE-MODEL.md)). v1
([`ADMIN-FRONTEND-GAPS.md`](ADMIN-FRONTEND-GAPS.md)) is done, apart from the
items in [`ADMIN-FRONTEND-STATUS.md`](ADMIN-FRONTEND-STATUS.md). Paths are
relative to `frontend/src/app/`.

## 1. Already changed (2026-10-05)

- **"Sign in with Google" on the login page** (`lib/google-identity.ts`, `apiClient.signInWithGoogle`, `useAuth().loginWithGoogle`).
  - Uses Google Identity Services for the Google ID token, then GCIP `accounts:signInWithIdp`.
  - Shown only when `VITE_GOOGLE_CLIENT_ID` is set; `VITE_GOOGLE_HOSTED_DOMAIN` is an optional account-chooser hint.
  - `workspace_sign_in_required` added to `lib/admin-errors.ts`.
- **Organization creation is platform-admin only.**
  - `createOrganization` now calls `POST /api/admin/organizations` with `{ name, firstAdminUserId }`.
  - The organizations panel shows "New organization" only when `isPlatformAdmin`.
  - Self-service `POST /api/organizations` returns 405.

## 2. Breaking or behavioural changes to absorb

> **Platform admins must use "Sign in with Google" with their Workspace account.** Platform access requires the token's `hd` to be on the backend's `INTERNAL_HD_ALLOWLIST` on every request. Email/password sign-ins never carry `hd`, so a platform admin signed in that way gets 403 `workspace_sign_in_required` and sees no platform permissions on `/me`. The Google button exists (section 1). It needs `VITE_GOOGLE_CLIENT_ID` and the GCIP blocking function (`backend/infra/gcip-blocking-function/README.md`). Organization and application features work with any sign-in method.

| Change | Where it bites | Do |
| --- | --- | --- |
| `Invoice.*` / `Report.*` permissions are **retired**: not listed by `GET /api/permissions`, can't be attached (400 `unknown_permission`), never resolve | Permission pickers, anything that hard-codes them | Nothing hard-codes them today. Don't add any. |
| New org-level permission `Organization.Update`; new platform permissions `Organization.Create`, `Organization.Deactivate`, `Application.Assign`, `Module.Manage`, `Catalog.Manage`; app-scope `Application.ManageAccess` | `can()` checks | Gate platform screens on these (section 4). |
| A deactivated organization answers **403 `organization_deactivated`** for its members, and disappears from `GET /api/organizations` and `/me` | Org pages | Show "This organization has been deactivated" for that reason. |
| Application-role grants come back from `GET /api/users/{id}` and `/members` with `applicationId` (and `expiresAt`) set | Role tables | Show the application next to the role; don't offer org-level revoke for app grants (use section 5). |
| New 403 `mfa_required` (only if the backend turns MFA on for external users) | Global error handling | Prompt the user to sign in again with their second factor. |

## 3. `/me` now carries the application tree

`GET /api/users/me` adds **`organizations`**. Each entry has:
- `organizationId`, `name`
- `permissions`: organization-level permissions
- `applications`: those the user holds something in, each with `applicationId`, `key`, `name`, `modules` (enabled modules the user holds something in) and `permissions`

`permissions[]` (per scope) now also carries `applicationId` for application scopes.

```ts
interface ModuleAccess { moduleId: number; key: string; name: string }
interface ApplicationAccess { applicationId: number; key: string; name: string; modules: ModuleAccess[]; permissions: string[] }
interface OrganizationAccess { organizationId: number; name: string; permissions: string[]; applications: ApplicationAccess[] }
// CurrentUser gains: organizations: OrganizationAccess[]
// ScopedPermissions gains: applicationId: number | null
```

Use it to build the app switcher and module navigation. Extend `useAdminAccess` with:
- `canIn(permission, organizationId, applicationId)`: application scope
- `isAppAdmin(organizationId, applicationId)`: holds `Application.ManageAccess` there

As before, this is UX only; the API re-checks everything. **Refetch `/me`** after any grant, revoke, membership or entitlement change.

## 4. Platform admin screens (`isPlatformAdmin`)

All of these live under `/api/admin/…` and need platform-scoped permissions. The platform admin doesn't need to be a member of the organization.

| Screen | Endpoints |
| --- | --- |
| All organizations (search, status filter, paging) | `GET /api/admin/organizations?search=&status=&page=&pageSize=` |
| Create organization: first admin by **user id or email** | `POST /api/admin/organizations` `{ name, firstAdminUserId }` or `{ name, firstAdminEmail }`. The email form returns `invitation.token`: show it once, to pass on |
| Deactivate / reactivate organization | `POST /api/admin/organizations/{organizationId}/deactivate` / `reactivate` |
| Application catalog (read) | `GET /api/catalog/applications`: applications → modules → permissions. **Empty until the first application ships** |
| Retire / reactivate an application or module | `PUT /api/catalog/applications/{applicationId}` / `…/modules/{moduleId}` `{ status: "Active" \| "Retired" }` |
| Assign / remove an application for an organization | `PUT` / `DELETE /api/admin/organizations/{organizationId}/applications/{applicationId}`. **Removing revokes every grant for it in that organization**, so confirm |
| Enable / disable a licensed module | `PUT` / `DELETE /api/admin/organizations/{organizationId}/applications/{applicationId}/modules/{moduleId}`. Disabling keeps grants |
| Appoint the first application admin | `PUT /api/admin/organizations/{organizationId}/applications/{applicationId}/members/{userId}/roles/{roleId}` with the "{App} Administrator" template |

## 5. Application admin screens (`isAppAdmin(org, app)`)

Under `/api/organizations/{organizationId}/applications/{applicationId}/…`. The organization must have the application, otherwise the API returns 404 `application_not_found`.

| Screen | Endpoints |
| --- | --- |
| Organization's applications (org members with `User.Read`) | `GET /api/organizations/{organizationId}/applications`: assigned apps + enabled modules |
| Application roles | `GET …/roles`: templates first (read-only, `organizationId: null`), then custom roles, each with `permissions` |
| Custom roles | `POST …/roles` `{ name, description }`; `PUT/DELETE …/roles/{roleId}`; compose with `POST …/roles/{roleId}/permissions` `{ permissionName }` and `DELETE …/roles/{roleId}/permissions/{name}`. Only this application's permissions; only ones you hold (`cannot_grant_unheld_permission`); never a role you hold (`cannot_modify_own_role`) |
| Grant / revoke | `PUT …/members/{userId}/roles/{roleId}` `{ expiresAt? }` (200, idempotent; a different `expiresAt` updates it); `DELETE …/members/{userId}/roles/{roleId}` (204). Target must be a member (`user_not_a_member`). The last app admin can't be revoked (409 `last_application_administrator`) |
| Access review | `GET …/access`: each user's roles and the permissions they resolve to right now |

**Organization admins can't grant application roles** (Decision 3). Hide the grant UI unless `isAppAdmin`.

## 6. Organization admin additions

| Screen | Endpoints |
| --- | --- |
| A member's grants (org-level + per application, with expiry) | `GET /api/organizations/{organizationId}/members/{userId}/grants` (`User.Read`) |
| **Invite by email** (replaces "add by user id" for new people) | `POST /api/organizations/{organizationId}/invitations` `{ email, grants: [{ roleId, applicationId? }], expiresInDays? }` returns `{ invitationId, token, expiresAt }`. Show the token once; it isn't retrievable later. Org-level pre-grants need `Role.Assign`; application pre-grants need `Application.ManageAccess` there |
| Pending invitations | `GET /api/organizations/{organizationId}/invitations` (no tokens; expired ones show `status: "Expired"`); revoke with `DELETE …/invitations/{invitationId}` |
| Accept (invitee) | `POST /api/invitations/accept` `{ token }`. Needs a sign-in whose email matches and is **verified** by the identity provider. Map `email_not_verified`, `invitation_email_mismatch`, `invitation_not_found`, and 410 `invitation_expired` |

## 7. Error reasons to add to `lib/admin-errors.ts`

| `reason` | Status | Suggested message |
| --- | --- | --- |
| `organization_deactivated` | 403 | This organization has been deactivated. |
| `application_not_found` | 404 | This organization doesn't have that application. |
| `application_not_assigned` | 409 | Assign the application to the organization first. |
| `module_not_found` | 404 | That module isn't part of this application. |
| `last_application_administrator` | 409 | Appoint another application administrator first. |
| `external_user_not_allowed` | 400 | External users can't hold platform roles. |
| `mfa_required` | 403 | Sign in again with your second factor. |
| `workspace_sign_in_required` | 403 | Platform administration requires signing in with your Google Workspace account. |
| `invitation_pending` | 409 | This email already has a pending invitation. |
| `invitation_not_found` | 404 | This invitation is no longer valid. |
| `invitation_expired` | 410 | This invitation has expired. Ask for a new one. |
| `email_not_verified` | 403 | Verify your email address, then accept again. |
| `invitation_email_mismatch` | 403 | Sign in with the email address the invitation was sent to. |
| `user_not_found` | 404 | No user with that id. They must sign in once first. |

## 8. Suggested order

1. Types, `/me` organizations tree, and `canIn` / `isAppAdmin`.
2. Error reasons.
3. Invitations (org admins), since they replace "add member by id".
4. Platform admin organization list, create (id or email) and deactivate.
5. Application admin screens: roles, grants, access review.
6. Platform catalog and entitlement screens (useful once the first application ships).
7. App switcher and module navigation driven by `/me`.
