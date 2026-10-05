# Admin API Gaps (Identity module)

> **Superseded by [`ADMIN-API-GAPS-v2.md`](ADMIN-API-GAPS-v2.md)**, which has the current summary and the remaining work. This file is kept as the v1 record.

What the admin portal needed from this API. **Every gap below is resolved**
(Tasks 1 to 10, 2026-10-04, not yet committed). Each entry names the files
that resolve it. Per-task detail, the endpoint authorization matrix and the
open decisions are in [`../ADMIN-API-STATUS.md`](../ADMIN-API-STATUS.md).
Read alongside [`AGENTS.md`](AGENTS.md) (conventions).

## Intentional deviations from this document

- **Org-scoped reads and writes moved under `{organizationId}`:** roles (`/api/organizations/{organizationId}/roles…`) and organizations (`{organizationId}`, not `{id}`), so the membership guard applies.
- **Still not under `{organizationId}`:** role writes (`/api/roles…`) and role assignment (`/api/users/{id}/roles`). They are authorized in their services and covered by the cross-tenant sweep; moving them is an open decision.
- **Idempotency status codes**, matching the existing attach/detach convention:
  - assign an already-held role: 200 (same body)
  - revoke an unheld role: 204
  - add an existing member: 204
  - remove a non-member: 204
  - enable/disable to the current status: 204

  None writes an audit row.
- **Cross-org ids are 404, not 403**, for role reads and role assignment, so other organizations' role ids can't be probed. An unknown **organization** id is 403 (the membership guard), the same as any organization the caller isn't in.
- **Role permission read-back** returns `[{ "name": … }]`, the same shape as `GET /api/permissions`.
- **New permission `Organization.Update`** (catalog id 18) gates organization renames.

## Critical: Admin.Access is never granted

**Resolved (Task 1).**
- `Persistence/Migrations/20261005012254_AddPlatformAdministratorAndRolePermissions.cs` adds `PlatformAdministrator` (Admin.Access, User.\*, Role.\*).
- The first holder is granted by `bootstrap-platform-admin` (`Application/Authorization/BootstrapPlatformAdministratorHandler.cs`, `ScrapGo.Core.Api/Composition/ScrapGoCommands.cs`).
- `IsOrganizationAdministratorAsync` was removed; `RoleService` is permission-based with an escalation guard.

Original finding:

`Permissions.AdminAccess` (`"Admin.Access"`, catalog id 17) is seeded by
migration but **no role holds it**. The built-in `OrganizationAdministrator`
role (id 1, platform-scoped) has **zero** `role_permissions` rows, and
`IAuthorizationQueries.IsOrganizationAdministratorAsync` checks the role
_name_, not permissions. So
`[RequirePermission(Permissions.AdminAccess, PlatformScope = true)]` currently
denies everyone. Fix this before adding a platform admin gate.

## Missing endpoints

### Roles

Done (Task 4), **at org-scoped routes** instead of the `/api/roles/...` paths
first proposed. Role reads are organization-scoped, and only a route value
named exactly `organizationId` engages `OrganizationMembershipGuardMiddleware`
and org-scoped `[RequirePermission]` resolution. Each action needs `Role.Read`
in that organization, and a role from another organization is 404.

- ~~`GET /api/roles`~~ → `GET /api/organizations/{organizationId}/roles`: that organization's active roles plus the platform built-ins (`organizationId` null).
- ~~`GET /api/roles/{id}`~~ → `GET /api/organizations/{organizationId}/roles/{id}`: `RoleDetailDto` (role plus permission names).
- ~~`GET /api/roles/{id}/permissions`~~ → `GET /api/organizations/{organizationId}/roles/{id}/permissions`: `[{ "name": ... }]`, the same shape as `GET /api/permissions`.

Writes stay at `POST/PUT/DELETE /api/roles...` (authorized inside `RoleService`) until the Task 10 sweep.

### Users

**Resolved (Tasks 5, 7, 8)** in `Api/Users/UsersController.cs`, with `UserReadService`, `UserRoleAssignmentService` and `UserStatusService`.

- `GET /api/users` — list users (paged/filtered).
- `GET /api/users/{id}` — user detail incl. roles.
- `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}` — assign/revoke. (`Role.Assign` exists but is unused.)
- `POST /api/users/{id}/disable`, `POST /api/users/{id}/enable` — status. (`User.Disable`/`Enable` exist but are not exposed.)

### Organizations

Every route here uses `{organizationId}`, not `{id}`, so the membership guard
and org-scoped `[RequirePermission]` apply.

- `GET /api/organizations/{organizationId}`: detail. **Done (Task 6)**, `User.Read` in that organization.
- `GET /api/organizations/{organizationId}/members`: paged members. **Done (Task 6)**, `User.Read` in that organization.
- `PUT /api/organizations/{organizationId}`: rename. **Done (Task 9)**, `Organization.Update` (new catalog permission, id 18) in that organization.
- `POST/DELETE /api/organizations/{organizationId}/members/{userId}`: add/remove membership. **Done (Task 9)**, `User.Update` in that organization. Adding grants no roles; removing also revokes the member's roles in that organization.

### Self

**Resolved (Task 5).** `/me` returns `roles` and per-scope `permissions` from `PermissionResolver.GetEffectiveAccessAsync` (`GetCurrentUserHandler`).

- Extend `GET /api/users/me` to include the caller's permissions/roles (needed for a real admin gate).

## Missing repository methods

**Resolved (Task 3).**
- DTO list reads are in `IAuthorizationQueries` (`ListUsersAsync`, `ListOrganizationsAsync`, `ListMembersAsync`, `ListRolesAsync`, plus `FindRoleAsync` and `GetOrganizationDetailAsync`).
- Tracked or aggregate methods are in the repositories.
- Duplicates of the query methods were not added (see `ADMIN-API-STATUS.md`, Task 3).

| Interface                 | Missing                                                                             |
| ------------------------- | ----------------------------------------------------------------------------------- |
| `IUserRepository`         | `ListAsync`/query, `GetByIdAsync`, `UpdateStatusAsync`                              |
| `IRoleRepository`         | `ListRolesAsync`, `GetRolePermissionsAsync`, `ListUserRolesAsync`, `RemoveUserRole` |
| `IOrganizationRepository` | `ListAsync`, `GetByIdAsync`, `Update`, `ListMembersAsync`, `RemoveMembership`       |
| `IAuthorizationQueries`   | `ListUsersAsync`, `ListMembersAsync`, `ListRolesAsync`                              |

## Contract changes

**Resolved (Task 2).**
- `RoleDto.OrganizationId`, plus `CurrentUserDto.Roles` and `.Permissions` (per scope).
- New: `UserSummaryDto`, `UserDetailDto`, `RoleDetailDto`, `OrganizationMemberDto`, `OrganizationDetailDto`, `AssignRoleRequest`, `UpdateOrganizationRequest`, `PagedResult<T>`, `PageRequest`.
- No user-status body is needed (separate enable/disable routes).

- `RoleDto` omits `OrganizationId` (needed to tell org vs platform roles).
- `CurrentUserDto` omits permissions/roles.
- New: `UserSummaryDto`, `UserDetailDto`, `RoleDetailDto` (role + permission
  names), `OrganizationMemberDto`, plus request DTOs for role assignment and
  user status.

## Missing audit events

**Resolved (Tasks 2, 7–9).** All seven exist in `IdentityAuditEventTypes` and are written by the handlers listed in `ADMIN-API-STATUS.md` ("Write-path consistency").

`IdentityAuditEventTypes` has no entries for: `user_disabled`, `user_enabled`,
`role_assigned`, `role_revoked`, `organization_updated`, `membership_added`,
`membership_removed`.

## Authorization wiring

**Resolved (Tasks 1, 4–10).**
- Every new admin endpoint uses `[RequirePermission]`.
- Platform vs organization admin is decided (Decision 1).
- A FallbackPolicy now enforces authentication on any action without attributes.
- See the authorization matrix in `ADMIN-API-STATUS.md`.

- No controller uses `[RequirePermission]` yet; new admin endpoints should.
- Org-scoped routes must carry `{organizationId}` to be covered by
  `OrganizationMembershipGuardMiddleware` and org-scoped permission resolution.
- Decide platform-scoped (`Admin.Access`, `PlatformScope = true`) vs org-scoped
  (`OrganizationAdministrator`) admin, and how the first platform admin is
  bootstrapped.

## Reuse (wire up, don't rebuild)

- `[RequirePermission]` + `PermissionResolver` + `PermissionAuthorizationHandler`
- `OrganizationMembershipGuardMiddleware`, `DisabledUserGateMiddleware`
- `RoleService` pattern: Application handler → outcome enum → controller mapping
- `ProblemResults.Create(status, title, detail, reason)`
- `IdentityUnitOfWork.ExecuteInTransactionAsync`, `IAuditLog<IdentityModule>`
- `PermissionScope(userId, organizationId)`
- Test fixture `IdentitySeed` helpers

## Suggested implementation order

1. **Bootstrap:** seed catalog permissions onto the built-in
   `OrganizationAdministrator` role (or define a platform-admin bootstrap).
2. **Read endpoints:** `GET /api/roles`, `GET /api/roles/{id}/permissions`,
   `GET /api/users`, `GET /api/organizations/{id}/members`.
3. **Write endpoints:** role assignment/revocation, user enable/disable,
   organization update, membership add/remove.
4. **Contracts:** extend `RoleDto`/`CurrentUserDto`; add new DTOs.
5. **Authorization:** apply `[RequirePermission]` consistently; settle platform
   vs org scope.
6. **Audit + integration tests** mirroring the `Roles/*` and `Organizations/*`
   specs.
