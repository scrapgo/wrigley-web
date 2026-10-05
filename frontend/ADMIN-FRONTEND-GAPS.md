# Admin Frontend Gaps

What the admin portal (`/admin`, `/settings`) must change now that the backend
admin API is complete. It is the frontend counterpart of
[`../backend/ADMIN-API-GAPS.md`](../backend/ADMIN-API-GAPS.md). The backend is
done; everything below is frontend work.

Every endpoint listed here exists and is covered by integration tests. Paths
are relative to `frontend/src/app/`.

## 1. Breaking changes: fix these first

| What changed | Where it bites | Fix |
| --- | --- | --- |
| Role-management 403s now use `reason: "missing_permission"`; `not_organization_administrator` is gone | `lib/admin-errors.ts` | Replace the entry, and add the new reasons in section 6. |
| Role writes need **`Role.Create` / `Role.Update` / `Role.Delete`** in the role's organization, not "is OrganizationAdministrator" | The roles panel assumes an org admin can always edit | Gate buttons on `/me` permissions (section 2). Expect new 403s: `cannot_modify_own_role`, `cannot_grant_unheld_permission`. |
| You cannot edit, delete or recompose **a role you hold yourself** | Role edit/delete/attach/detach | Disable these actions for roles in the caller's own `/me` `roles` (same organization). |
| You can only attach a permission **you hold yourself** in that organization | Permission picker in role composition | Offer only permissions in the caller's `/me` permissions for that organization. |

No existing route was removed or renamed. `POST/PUT/DELETE /api/roles…` and
`/api/roles/{id}/permissions` work as before.

## 2. Replace the interim admin gate with real permissions

`hooks/useAdminAccess.ts` currently shows admin features to **every**
authenticated user. `GET /api/users/me` now reports what the caller can do:

```ts
interface CurrentUser {
    id: number
    identityPlatformUid: string
    email: string
    status: string
    classification: string
    roles: { roleId: number; name: string; organizationId: number | null }[]
    // One entry per scope the caller holds anything in. organizationId null = platform scope.
    permissions: { organizationId: number | null; permissions: string[] }[]
}
```

- **Platform admin** (users, enable/disable, platform roles): `Admin.Access` in the entry where `organizationId === null`.
- **Organization admin views:** the permission in **that organization's** entry. Examples:
  - `User.Read` to see members
  - `Role.Read` to list roles
  - `Organization.Update` to rename
- **Never flatten scopes.** `Role.Update` in org 12 grants nothing in org 15 or at platform scope.
- An organization appears only while the caller's membership there is active.
- Add a helper such as `can(permission, organizationId | null)` and use it for nav items, route guards and buttons.
- Treat this as UX only: the API re-checks everything and answers 403 regardless.
- **After any role or membership change**, refetch `/me` (invalidate its query). Permissions take effect on the next request; nothing is cached server-side.

Update the `CurrentUser` type in `lib/api-client.ts`, then remove the "Interim" rows in `AGENTS.md` (Admin Portal table).

## 3. Delete the browser-local role registry

The read endpoints it worked around now exist. Remove `lib/role-registry.ts`
and every `roleRegistry.*` call in `hooks/useAdminQueries.ts`, and replace
`useRegisteredRoles` with server queries.

| Purpose | Endpoint | Response |
| --- | --- | --- |
| List roles usable in an org | `GET /api/organizations/{organizationId}/roles` | `Role[]`: that org's custom roles **plus** the built-ins (`organizationId: null`), built-ins first |
| One role + its grants | `GET /api/organizations/{organizationId}/roles/{id}` | `RoleDetail` |
| A role's grants only | `GET /api/organizations/{organizationId}/roles/{id}/permissions` | `{ name: string }[]` (same shape as `GET /api/permissions`) |

```ts
interface Role { id: number; name: string; description: string; organizationId: number | null }
interface RoleDetail extends Role { permissions: string[] }
```

- **Roles are per organization now.** Pick an organization first (from `GET /api/organizations`), then list its roles. Query keys must include `organizationId`.
- **Built-in roles** (`organizationId === null`: `OrganizationAdministrator`, `PlatformAdministrator`) are read-only. The write routes answer 404 for them, so hide edit/delete and show them as "Built-in".
- After create/update/delete/attach/detach, invalidate that organization's role queries instead of writing to local storage.
- All three reads need `Role.Read` in the organization (403 otherwise). A role from another organization is **404**.

## 4. New screens

### 4.1 User administration (platform admins)

Needs `Admin.Access` and `User.Read` at platform scope to view, and `User.Update` at platform scope to enable/disable.

| Action | Endpoint | Notes |
| --- | --- | --- |
| List users | `GET /api/users?search=&status=&page=&pageSize=` | `PagedResult<UserSummary>`. `search` matches email (case-insensitive substring); `status` is `active` or `disabled`; `page` starts at 1, `pageSize` defaults to 25 (max 100). Bad values: 400 `invalid_request`. |
| User detail | `GET /api/users/{id}` | `UserDetail`, including roles in every scope |
| Disable / enable | `POST /api/users/{id}/disable`, `POST /api/users/{id}/enable` | 204, idempotent. Disabling yourself: 403 `cannot_disable_self`. Disabling the last active platform admin: 409 `last_platform_administrator`. The disabled user's **next** request is rejected. |

```ts
interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
interface UserSummary { id: number; email: string; displayName: string | null; status: 'Active' | 'Disabled'; createdAt: string }
interface UserDetail extends UserSummary { roles: { roleId: number; name: string; organizationId: number | null }[] }
```

- `displayName` is **always null today**; the backend stores no display name yet. Render the email.
- There is **no platform-wide organization list** yet (see `backend/ADMIN-API-GAPS-v2.md`). The admin's org picker can only offer their own organizations from `GET /api/organizations`.

### 4.2 Role assignment (org admins and platform admins)

| Action | Endpoint | Notes |
| --- | --- | --- |
| Assign | `POST /api/users/{userId}/roles`, body `{ roleId, organizationId? }` | 200 with `{ roleId, name, organizationId }`. Already held: the same 200 (idempotent). |
| Revoke | `DELETE /api/users/{userId}/roles/{roleId}?organizationId=` | 204; also 204 if the role wasn't held |

- **`organizationId` decides the scope.** Omit it only for platform scope; platform-scope changes need `Admin.Access`.
- Org scope needs `Role.Assign` in that organization.
- The target user must be an active member of the organization (`user_not_a_member` otherwise). Add them first (4.3).
- You can only assign a role whose permissions you hold yourself (`cannot_grant_unheld_permission`).
- `PlatformAdministrator` is platform-scope only, and `OrganizationAdministrator` org-scope only (`role_scope_mismatch`).
- An organization's last OrganizationAdministrator, or the platform's last PlatformAdministrator, can't be revoked (409).

### 4.3 Organization detail and members (org admins)

All under `/api/organizations/{organizationId}`. The caller must be an
**active member** of that organization; otherwise every call is 403
`no_active_membership`, including for an organization id that doesn't exist.

| Action | Endpoint | Permission | Notes |
| --- | --- | --- | --- |
| Detail | `GET /api/organizations/{organizationId}` | `User.Read` | `OrganizationDetail` |
| Rename | `PUT /api/organizations/{organizationId}`, body `{ name }` | `Organization.Update` | 200 with the updated detail. The slug never changes. 400 `invalid_name`. |
| Members | `GET /api/organizations/{organizationId}/members?page=&pageSize=` | `User.Read` | `PagedResult<OrganizationMember>`, active members only, each with their roles **in this org** |
| Add member | `POST /api/organizations/{organizationId}/members/{userId}` | `User.Update` | 204, idempotent. **Grants no roles**; assign them separately (4.2). Unknown user: 404. |
| Remove member | `DELETE /api/organizations/{organizationId}/members/{userId}` | `User.Update` | 204, idempotent. Also removes all their roles in this org. Last org admin: 409 `last_organization_administrator`. |

```ts
interface OrganizationDetail { id: number; name: string; slug: string; status: string; createdAt: string; activeMemberCount: number }
interface OrganizationMember {
    userId: number; email: string; displayName: string | null; joinedAt: string
    roles: { roleId: number; name: string; organizationId: number | null }[]
}
```

- **Adding a member needs a user id.** Org admins can't search users (`GET /api/users` is platform-only), so there is no "invite by email" yet. See the backend v2 doc.
- `POST /api/organizations` returns `Location: /api/organizations/{id}`, which now resolves.

## 5. Updated feature status

Replaces the table in `AGENTS.md` ("Admin Portal — Next Steps").

| Frontend feature | Endpoints | Status |
| --- | --- | --- |
| Admin nav + route gate | `GET /api/users/me` (`roles`, `permissions`) | **Unblocked**: replace the interim gate (section 2) |
| Organizations list/create | `GET/POST /api/organizations` | Built |
| Organization detail/rename | `GET/PUT /api/organizations/{organizationId}` | **Unblocked** |
| Organization members | `GET/POST/DELETE /api/organizations/{organizationId}/members…` | **Unblocked** (add by user id only) |
| Roles table | `GET /api/organizations/{organizationId}/roles` | **Unblocked**: delete the local registry (section 3) |
| Role create/edit/delete | `POST/PUT/DELETE /api/roles` | Built; adjust for the new 403 reasons |
| Role permission composition + read-back | `/api/roles/{id}/permissions`, `GET …/roles/{id}/permissions` | **Unblocked** |
| Permission catalog | `GET /api/permissions` | Built (now includes `Organization.Update`) |
| User administration | `GET /api/users`, `GET /api/users/{id}`, enable/disable | **Unblocked** (platform admins) |
| Role assignment | `POST/DELETE /api/users/{id}/roles` | **Unblocked** |
| Platform-wide organization list | none yet | Blocked: backend v2 |
| Invite / add member by email | none yet | Blocked: backend v2 |
| Settings / profile | `GET /api/users/me`, linked-provider link | Built |

## 6. Error reasons to map in `lib/admin-errors.ts`

Errors are RFC 7807 ProblemDetails with a `reason`. These are new or changed:

| `reason` | Status | Suggested message |
| --- | --- | --- |
| `missing_permission` | 403 | You don't have permission to do that in this organization. |
| `cannot_modify_own_role` | 403 | You can't change a role you hold yourself. Ask another administrator. |
| `cannot_grant_unheld_permission` | 403 | You can only grant permissions you hold yourself. |
| `platform_admin_required` | 403 | Only a platform administrator can change platform roles. |
| `cannot_disable_self` | 403 | You can't disable your own account. |
| `role_not_found` | 404 | That role isn't available in this organization. |
| `role_scope_mismatch` | 400 | That role can't be assigned in this scope. |
| `user_not_a_member` | 400 | Add the user to the organization first. |
| `last_platform_administrator` | 409 | Assign another platform administrator first. |
| `last_organization_administrator` | 409 | Assign another organization administrator first. |
| `invalid_request` | 400 | Already mapped; now also used for bad paging and filter values. |
| ~~`not_organization_administrator`~~ | n/a | Remove it; the API no longer sends it. |

## 7. Suggested order

1. Update the types, remove `not_organization_administrator`, and map the new reasons.
2. Gate the admin UI on `/me` permissions (`can(permission, organizationId)`), and refetch `/me` after any role or membership change.
3. Switch roles to server reads and delete `role-registry.ts`.
4. Build organization detail, rename and members.
5. Build role assignment.
6. Build platform user administration.
7. Update the Admin Portal table in `AGENTS.md`.
