# Admin API Gaps v2 (Identity module)

Supersedes [`ADMIN-API-GAPS.md`](ADMIN-API-GAPS.md). Every gap in v1 is resolved;
this file summarizes what exists now and lists what is still worth doing.
Per-task detail, the endpoint authorization matrix and the decision log are
in [`../ADMIN-API-STATUS.md`](../ADMIN-API-STATUS.md). The frontend
counterpart is [`../frontend/ADMIN-FRONTEND-GAPS.md`](../frontend/ADMIN-FRONTEND-GAPS.md).

## 1. Summary of v1 (done)

**Status:** Tasks 1–10, 2026-10-04/05, **not yet committed**.

**Tests:** 312 passed, 0 failed (Identity 282, QuickbaseEngine 30). The starting baseline was 127.

### Admin endpoints

| Area | Endpoints | Authorization |
| --- | --- | --- |
| Self | `GET /api/users/me` (now with `roles` and per-scope `permissions`) | authenticated, not disabled |
| Users (platform) | `GET /api/users`, `GET /api/users/{id}`, `POST /api/users/{id}/disable`, `/enable` | `User.Read` / `User.Update` at platform scope |
| Role assignment | `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}?organizationId=` | `Role.Assign` in the org (or `Admin.Access` + `Role.Assign` at platform scope), checked in the service |
| Organization | `GET/PUT /api/organizations/{organizationId}`, `GET …/members`, `POST/DELETE …/members/{userId}` | membership guard + `User.Read` / `Organization.Update` / `User.Update` in the org |
| Role reads | `GET /api/organizations/{organizationId}/roles`, `…/{id}`, `…/{id}/permissions` | membership guard + `Role.Read` in the org |
| Role writes (unchanged routes) | `POST/PUT/DELETE /api/roles…`, `…/permissions` | `Role.Create/Update/Delete` in the role's org, checked in `RoleService` |

### Platform guarantees

- **Admin.Access works.**
  - The built-in `PlatformAdministrator` role holds `User.*`, `Role.*` and `Admin.Access`.
  - The first holder is granted by the one-time, audited CLI command `bootstrap-platform-admin --uid`.
  - `OrganizationAdministrator` holds everything except `Admin.Access`.
- **Deny-by-default, enforced three ways:**
  - an authenticated FallbackPolicy
  - no grants on sign-in or membership add
  - permission-only checks with no role-name checks
- **Escalation guard on every role write path:** you can't grant a permission you don't hold, and you can't modify a role you hold.
- **Lock-out protection:** the last active PlatformAdministrator, and an organization's last active OrganizationAdministrator, can't be revoked, removed or (platform admins only) disabled.
- **Every write is one transaction with its audit row.** The audit events are:

  | Area | Events |
  | --- | --- |
  | Users | `user_provisioned`, `provider_linked`, `user_disabled`, `user_enabled` |
  | Organizations | `organization_created`, `organization_updated` |
  | Membership | `membership_added`, `membership_removed` |
  | Roles | `role_created`, `role_updated`, `role_deleted`, `role_permission_attached`, `role_permission_detached` |
  | Assignment | `role_assigned`, `role_revoked` |
  | Bootstrap | `platform_administrator_bootstrapped` |

- **Tenant isolation:** `Organizations/CrossTenantSweep.cs` runs every org-scoped endpoint as org A's admin against org B's ids, and asserts 403/404, no leakage, and no change.

### Migrations

These must be applied to each database, Identity context:

1. `20261005012254_AddPlatformAdministratorAndRolePermissions`
2. `20261005044925_AddOrganizationUpdatePermission` (catalog id 18, `Organization.Update`)

### v1 audit

All 15 endpoints, the self extension, the contract changes, the 7 audit events and the authorization wiring from v1 exist and are tested. The repository methods were delivered with a deliberate split: DTO list reads went to `IAuthorizationQueries` instead of duplicating them in repositories. Deviations from v1 (route moves, idempotency codes, 404-not-403 for foreign ids) are listed at the top of v1.

## 2. Open decisions (owner: product/security)

Carried over from `ADMIN-API-STATUS.md`, Decision 6. Nothing was changed for these.

| # | Decision | Today | Recommendation |
| --- | --- | --- | --- |
| D1 | Move `/api/roles…` writes and `/api/users/{id}/roles` under `/api/organizations/{organizationId}/…` | Secure (service-checked, swept), but not covered by the membership guard | **Yes, in a coordinated release.** Add the org-scoped routes, keep the old ones as deprecated aliases for one release, and switch the frontend. Uniform routes mean one rule for every reviewer. |
| D2 | Should any signed-in user be able to create an organization and become its admin? | Yes, scoped to that org only | Decide by business model. If orgs are provisioned by ScrapGo staff, require a platform permission on `POST /api/organizations`. |
| D3 | Disabling a user who is an org's last OrganizationAdministrator | Allowed (only the platform lock-out is blocked) | Block it with 409 `last_organization_administrator` for consistency, or allow a platform-admin override. |
| D4 | `GET /api/organizations/{organizationId}` and `/members` use `User.Read` | No `Organization.Read` exists | Add `Organization.Read` (append id 19) if org detail should be visible without people access. |

## 3. Remaining gaps (recommended next work)

In priority order. Each one names the pieces that already exist.

### P1: needed for the admin portal to be complete

1. **Platform-wide organization list.**
   - Platform admins can't see organizations they don't belong to.
   - Add `GET /api/admin/organizations?search=&status=&page=&pageSize=` with `[RequirePermission(Admin.Access, PlatformScope = true)]` (plus `User.Read`).
   - The query exists: `IAuthorizationQueries.ListOrganizationsAsync` with `OrganizationListFilter`.
   - Decision 1 already reserved this route shape.
2. **Find a user to add to an organization.**
   - Org admins can only add members by user id, and `GET /api/users` is platform-only.
   - Options:
     - (a) `POST /api/organizations/{organizationId}/members` with body `{ email }`, resolving an existing user by email. It answers the same 204 whether or not the email exists, to avoid account enumeration.
     - (b) An invitation flow: `invitations` table, token, accept endpoint, expiry, audit. This was ported from identity-platform but not migrated.
   - (a) is a small step; (b) is the long-term answer.
3. **Organization disable/enable.**
   - `Organization.Disable` exists in the domain but is not exposed. A platform-admin action is needed to suspend a customer.
   - Decide whether a disabled organization should also be denied by the membership guard. Today the guard checks membership status only, not organization status.

### P2: correctness and operability

4. **Display names.**
   - `UserSummaryDto` / `UserDetailDto` / `OrganizationMemberDto.DisplayName` are always null.
   - Add a `users.display_name` column, capture the GCIP `name` claim in `ProvisionCurrentUserHandler`, and extend `ListUsersAsync` search to cover it.
5. **Audit log read API.**
   - Admins can't see history. Add `GET /api/organizations/{organizationId}/audit` (org events, `User.Read` or a new `Audit.Read`) and a platform equivalent, paged.
   - Rows already carry the actor, organization and metadata.
6. **Permission cache.**
   - `IPermissionCache` is a pass-through. Invalidation calls are already wired on every write path (role edits, assignment, membership removal).
   - When Redis lands, implement the cache and run the full suite. The "next request sees the change" specs will catch a missed invalidation.
7. **Publish the error contract.**
   - The frontend maps `reason` strings by hand.
   - Expose the reason codes in OpenAPI (a `reason` enum on ProblemDetails, or one documented list), so the client can be generated or at least checked.

### P3: hardening and scale

8. **Search indexing:** user and org search is `ILIKE '%term%'`, which can't use an index. Add `pg_trgm` GIN indexes on `users.email` and `organizations.name` once the tables exceed a few thousand rows.
9. **Rate limiting:** not ported from identity-platform. Apply it at least to `/api/users/me` (provisioning), linking, and the admin write endpoints.
10. **Role deletion with assignments:** `DELETE /api/roles/{id}` returns 409 `role_still_assigned`. Consider an admin "revoke from all and delete" action, audited per revocation, for cleaning up roles.
11. **Concurrency of the last-admin checks:** they serialize on a row lock on the built-in role. That is correct, but every org's last-admin check contends on role 1. That's fine at current scale; revisit if org-admin churn gets heavy (for example, lock the organization row instead).
12. **Docs housekeeping:**
    - v1 keeps its original "finding" text under each "Resolved" heading for history. Treat this file as current.
    - Update the Authorization Model in `AGENTS.md` once D1 to D4 are decided.

## 4. Suggested next tasks

| # | Task | Size |
| --- | --- | --- |
| 11 | Platform organization list (P1.1) | S: the query exists |
| 12 | Add member by email (P1.2a) | S |
| 13 | Organization disable/enable + guard decision (P1.3) | M |
| 14 | Decide D1 to D4; implement D1 with deprecated aliases | M, coordinate with frontend |
| 15 | Display name capture (P2.4) | S: migration + provisioning |
| 16 | Audit log read API (P2.5) | M |
| 17 | Invitation flow (P1.2b) | L |
