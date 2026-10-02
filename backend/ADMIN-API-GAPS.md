# Admin API Gaps (Identity module)

What the admin portal needs from this API, and what is missing today.
Read alongside [`AGENTS.md`](AGENTS.md) (conventions) before implementing.

## Critical: Admin.Access is never granted

`Permissions.AdminAccess` (`"Admin.Access"`, catalog id 17) is seeded by
migration but **no role holds it**. The built-in `OrganizationAdministrator`
role (id 1, platform-scoped) has **zero** `role_permissions` rows, and
`IAuthorizationQueries.IsOrganizationAdministratorAsync` checks the role
_name_, not permissions. So
`[RequirePermission(Permissions.AdminAccess, PlatformScope = true)]` currently
denies everyone. Fix this before adding a platform admin gate.

## Missing endpoints

### Roles

- `GET /api/roles` — list roles (org-scoped + platform built-ins). Blocks any role table.
- `GET /api/roles/{id}` — role detail.
- `GET /api/roles/{id}/permissions` — read back a role's grants. Blocks showing what a role grants.

### Users

- `GET /api/users` — list users (paged/filtered).
- `GET /api/users/{id}` — user detail incl. roles.
- `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}` — assign/revoke. (`Role.Assign` exists but is unused.)
- `POST /api/users/{id}/disable`, `POST /api/users/{id}/enable` — status. (`User.Disable`/`Enable` exist but are not exposed.)

### Organizations

- `GET /api/organizations/{id}`, `PUT /api/organizations/{id}` — detail/update.
- `GET /api/organizations/{id}/members` — list members.
- `POST/DELETE /api/organizations/{id}/members/{userId}` — add/remove membership.

### Self

- Extend `GET /api/users/me` to include the caller's permissions/roles (needed for a real admin gate).

## Missing repository methods

| Interface                 | Missing                                                                             |
| ------------------------- | ----------------------------------------------------------------------------------- |
| `IUserRepository`         | `ListAsync`/query, `GetByIdAsync`, `UpdateStatusAsync`                              |
| `IRoleRepository`         | `ListRolesAsync`, `GetRolePermissionsAsync`, `ListUserRolesAsync`, `RemoveUserRole` |
| `IOrganizationRepository` | `ListAsync`, `GetByIdAsync`, `Update`, `ListMembersAsync`, `RemoveMembership`       |
| `IAuthorizationQueries`   | `ListUsersAsync`, `ListMembersAsync`, `ListRolesAsync`                              |

## Contract changes

- `RoleDto` omits `OrganizationId` (needed to tell org vs platform roles).
- `CurrentUserDto` omits permissions/roles.
- New: `UserSummaryDto`, `UserDetailDto`, `RoleDetailDto` (role + permission
  names), `OrganizationMemberDto`, plus request DTOs for role assignment and
  user status.

## Missing audit events

`IdentityAuditEventTypes` has no entries for: `user_disabled`, `user_enabled`,
`role_assigned`, `role_revoked`, `organization_updated`, `membership_added`,
`membership_removed`.

## Authorization wiring

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
