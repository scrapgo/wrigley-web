# Admin API Status (Identity module audit)

Audit of `backend/src/Modules/Identity` against [`backend/ADMIN-API-GAPS.md`](backend/ADMIN-API-GAPS.md), done on 2026-10-04 at commit `1c491b7`. No source files were changed. Paths below are relative to `backend/`.

## Summary

- **The admin API surface is almost entirely missing.** Of the 15 endpoints in `ADMIN-API-GAPS.md`, 0 exist. Of the 18 repository methods, 0 exist. Of the 7 audit event types, 0 exist. The two DTO changes aren't made. What exists is the Identity core: 10 endpoints across 4 controllers, the `[RequirePermission]` engine, and two request gates. All 127 tests pass.
- **The critical bug is confirmed, and it is wider than `ADMIN-API-GAPS.md` says.** No role holds `Admin.Access`. Seeding permissions onto `OrganizationAdministrator` would **not** fix `PlatformScope = true`: that role is only ever assigned *per organization* (`UserRole.OrganizationId` set), and a platform-scoped check only counts assignments whose organization is null. Nothing in production creates a platform role or a platform assignment, so every `PlatformScope = true` gate denies everyone.
- **There is no admin bypass in this codebase.** The bypass and all the other leftover RBAC named in the brief (Admin / InvoiceAdmin / InvoiceUser roles, `perm:{key}` policies) live in the separate **liverpool** `IntegrationService` repo, not in wrigley-web. `PendingRoleGrant`, `UserProvisioningState` and `GET /api/v1/me` exist in neither repo. Within wrigley-web there is one authorization path, but it splits two ways: `RoleService` authorizes by **role name**, while `[RequirePermission]` authorizes by **permission**.
- **No production controller uses `[RequirePermission]` today.** Only the test `ProbeController` does. Every current endpoint is plain `[Authorize]`, with role management checked by name inside `RoleService`.
- **Two route-shape traps in `ADMIN-API-GAPS.md`:**
  - It specifies `GET/PUT /api/organizations/{id}` and `/api/organizations/{id}/members`. The membership guard and organization-scoped permission resolution only fire on a route value named exactly `organizationId`, so those routes would be silently unguarded.
  - It specifies org-level role assignment at `/api/users/{id}/roles`, which carries no organization at all.

## Architecture map

### Projects and layers

| Layer | Project (under `src/Modules/Identity/`) | Identity code |
| --- | --- | --- |
| Domain | `ScrapGo.Core.Modules.Identity.Domain` | `Users/` (User, LinkedCredential, enums), `Organizations/` (Organization, OrganizationMembership, enums), `Authorization/` (Role, Permission, RolePermission, UserRole, `Permissions` catalog, `DefaultRoleNames`) |
| Application | `ScrapGo.Core.Modules.Identity.Application` | `Abstractions/` (repository, query, unit-of-work, cache and token interfaces), `Users/` (provision, status gate, link handlers; `CurrentUserDto`), `Organizations/` (create, list-mine, membership-guard handlers), `Roles/` (`RoleService`, `RoleContracts`), `Authorization/` (`PermissionResolver`, `ListPermissionsHandler`), `IdentityAuditEventTypes` |
| Infrastructure | `ScrapGo.Core.Modules.Identity.Infrastructure` | `Persistence/` (`IdentityDbContext`, repositories, `AuthorizationQueries`, `IdentityUnitOfWork`, configurations, migrations), `Authentication/` (GCIP JWT validation), `Authorization/PassThroughPermissionCache` |
| Api | `ScrapGo.Core.Modules.Identity.Api` | `Users/`, `Organizations/`, `Roles/`, `Permissions/` controllers; `Authorization/` (`RequirePermissionAttribute`, `PermissionAuthorizationHandler`, `OrganizationContextAuthorizationResultHandler`, `OrganizationRouteValues`, `HttpUserContext`); `Middleware/` (two gates) |
| Host | `src/ScrapGo.Core.Api/Composition/` | `ScrapGoModulesServiceCollectionExtensions` (registers the module), `ScrapGoHostingExtensions` (pipeline order) |
| Tests | `tests/Modules/ScrapGo.Core.Modules.Identity.IntegrationTests` | `Fixtures/`, `Authentication/`, `Authorization/`, `Organizations/`, `Roles/`, `Users/` |

Migrations (`…Infrastructure/Persistence/Migrations/`): `20260928232816_InitialIdentity`, `20260929043451_AddOrganizationsAndRbac`.

### Existing endpoints

| Verb | Route | Controller action | Authorization |
| --- | --- | --- | --- |
| GET | `/api/users/me` | `UsersController.GetMe` | `[Authorize]` (class) |
| POST | `/api/users/me/linked-providers/link` | `UsersController.LinkProvider` | `[Authorize]` |
| POST | `/api/organizations` | `OrganizationsController.Create` | `[Authorize]` |
| GET | `/api/organizations` | `OrganizationsController.ListMine` (the caller's orgs only, not an admin list) | `[Authorize]` |
| POST | `/api/roles` | `RolesController.Create` | `[Authorize]` + in-service OrganizationAdministrator-by-name check |
| PUT | `/api/roles/{id:int}` | `RolesController.Update` | same |
| DELETE | `/api/roles/{id:int}` | `RolesController.Delete` | same |
| POST | `/api/roles/{id:int}/permissions` | `RolesController.AttachPermission` | same |
| DELETE | `/api/roles/{id:int}/permissions/{name}` | `RolesController.DetachPermission` | same |
| GET | `/api/permissions` | `PermissionsController.List` | `[Authorize]` |

Plus anonymous `/healthz`, `/healthz/ready` and (when enabled) `/swagger`. No production route carries `{organizationId}`, so the membership guard currently never fires outside tests.

### The `RoleService` pattern (template for every new endpoint)

1. **Contracts** in `…Application/Roles/RoleContracts.cs`:
   - command records: `CreateRoleCommand` (line 9) through `RolePermissionCommand` (15), each carrying `ActorUid`
   - the result DTO `RoleDto` (17)
   - outcome enums: `RoleMutationOutcome` (22: Success / InvalidRequest / Forbidden / NotFound / DuplicateName), `RoleDeletionOutcome` (41), `RolePermissionOutcome` (51)
   - the result record `RoleMutationResult(Outcome, Role?)` (39)
2. **Handler** `…Application/Roles/RoleService.cs`, registered as scoped in `IdentityApplicationServiceCollectionExtensions.cs:22`. Each method does the following, in order:
   1. validates input, returning `InvalidRequest`
   2. loads the target through `IRoleRepository`, returning `NotFound`
   3. authorizes through `ResolveAdministratorAsync` (189), returning `Forbidden`
   4. mutates the domain entity
   5. stages an audit row with `IAuditLog<IdentityModule>.Record` (49, 83, 119, 172)
   6. saves through `IUnitOfWork`, mapping a unique-index race to `DuplicateName` via `UniqueConstraintViolationException` (`TrySaveRoleAsync`, 196–206)
   7. invalidates the permission cache for every holder of the role (`InvalidateHoldersAsync`, 209)
3. **Controller** `…Api/Roles/RolesController.cs`:
   - reads the caller's UID with `User.GetIdentityPlatformUid()`
   - builds the command and calls one service method (43, 65, 86, 114, 134)
   - maps the outcome: success goes to `Created` / `Ok` / `NoContent`; failures go through `MutationProblem` (141) / `PermissionProblem` (156) to `ProblemResults.Create(status, title, detail, reason)` or `ControllerProblems` (`…Api/ControllerProblems.cs`)
4. **Tests** in `tests/…/Roles/CustomRoleCrud.cs` drive the real HTTP pipeline and assert on the database through `fixture.DbContext`.

## Authorization pipeline

Middleware order (`src/ScrapGo.Core.Api/Composition/ScrapGoHostingExtensions.cs`):

1. `UseRouting` (89)
2. `UseAuthentication` (95)
3. `UseIdentityModule` (100), which runs `DisabledUserGateMiddleware` then `OrganizationMembershipGuardMiddleware` (`…Api/IdentityModuleApplicationBuilderExtensions.cs:17-18`)
4. `UseAuthorization` (102)

1. **Authentication.** JWT bearer validates the GCIP ID token: issuer and audience equal the project, RS256 only, `exp - iat ≤ 1h`, and `MapInboundClaims = false` (`…Infrastructure/IdentityInfrastructureServiceCollectionExtensions.cs`).
2. **`DisabledUserGateMiddleware`** (`…Api/Middleware/DisabledUserGateMiddleware.cs`). For an authenticated caller it runs `EvaluateUserStatusGateHandler`. A user row whose status isn't Active gets 403 `user_disabled` and a `denied_disabled_user` audit row. A missing user row passes, so first-time callers can provision.
3. **`OrganizationMembershipGuardMiddleware`** (`…Api/Middleware/OrganizationMembershipGuardMiddleware.cs:30-46`). It runs only when the matched route has a value named exactly `organizationId` (`OrganizationRouteValues.cs:12`). A value that isn't an integer, or a caller without an active membership, gets 403 `no_active_membership` and a `denied_cross_tenant_access` audit row. `{id}` or `{orgId}` are not recognised.
4. **`[RequirePermission(name, PlatformScope)]`** (`…Api/Authorization/RequirePermissionAttribute.cs`) implements `IAuthorizationRequirementData`, so no named policies are registered. It yields `PermissionRequirement`, which `PermissionAuthorizationHandler` (`…Api/Authorization/PermissionAuthorizationHandler.cs:19-40`) evaluates:
   - `PlatformScope = false` (default): reads `{organizationId}` from the route. If it's missing, the handler calls `context.Fail(organization_context_required)`, which `OrganizationContextAuthorizationResultHandler` turns into 400 rather than 403.
   - `PlatformScope = true`: the organization is `null`.
   - It then calls `PermissionResolver.HasPermissionAsync(uid, organizationId, permission)`.
5. **`PermissionResolver`** (`…Application/Authorization/PermissionResolver.cs:17-35`) maps the UID to a user id, then a `PermissionScope(userId, organizationId)`. It checks `IPermissionCache` (a pass-through), then calls `AuthorizationQueries.GetPermissionNamesAsync` (`…Infrastructure/Persistence/AuthorizationQueries.cs:29-47`). That query selects the user's **active** roles in exactly that scope: `ur.OrganizationId == organizationId`, or `ur.OrganizationId == null` for platform scope (36–38). It then returns the union of their permission names. It does **not** check membership itself; it relies on the guard in step 3.

### Critical bug: confirmed

- `Admin.Access` is catalog id 17: `…Domain/Authorization/Permissions.cs:34`, and the migration `20260929043451_AddOrganizationsAndRbac.cs:200` contains `{ 17, …, "Admin.Access", … }`.
- `OrganizationAdministrator` is role id 1, seeded with **no** permissions:
  - `…Infrastructure/Persistence/Configurations/RoleConfiguration.cs:76-85` has `HasData(new { Id = OrganizationAdministratorRoleId, Name = DefaultRoleNames.OrganizationAdministrator, … OrganizationId = (int?)null, … })`
  - the migration line 207 inserts `roles` row 1
  - the migrations contain **0** `InsertData` calls for `role_permissions`
- `IsOrganizationAdministratorAsync` checks the role **name** (`AuthorizationQueries.cs:14-27`):
  ```csharp
  && ur.OrganizationId == organizationId
  && ur.Role.OrganizationId == null
  && ur.Role.Name == DefaultRoleNames.OrganizationAdministrator
  && ur.Role.Status == RoleStatus.Active,
  ```
- **Wider than reported:** the only production role grant is `CreateOrganizationHandler.cs:64`, `roles.AddUserRole(UserRole.Assign(creator.Id, organizationAdministratorRoleId, organization.Id, now))`, which is organization-scoped. `Role.CreatePlatformRole` (`Role.cs:57`) is used only by the test seed (`IdentitySeed.cs:86`). So no production path can ever satisfy `PlatformScope = true`, whatever permissions role 1 carries.

### Admin bypass and leftover RBAC

| Item | Where it is | Relation to Identity |
| --- | --- | --- |
| Admin bypass | **liverpool** `service/src/IntegrationService.Api/Authorization/IPermissionService.cs:10-12`: `IsAdmin => Roles.Contains("Admin")`; `HasPermission(p) => IsAdmin \|\| Permissions.Contains(p)` | **Not present in wrigley-web.** `PermissionAuthorizationHandler` has no short-circuit. Re-introducing one would conflict with the deny-by-default, permission-only model and with the AGENTS.md rule against role-name checks. |
| Admin / InvoiceAdmin / InvoiceUser roles | liverpool `IntegrationService.Core/Data/Configurations/RoleConfiguration.cs:15-22` (with "Admin intentionally has no RolePermission rows - it bypasses…") | Not in wrigley-web. The closest analogue is the generic `Invoice.*` permissions copied into the catalog from identity-platform. |
| `perm:{key}` dynamic policies | liverpool `IntegrationService.Api/Authorization/PermissionPolicyProvider.cs`; used by `InvoicesController`, `UserRolesController` | Not in wrigley-web. Replaced by `[RequirePermission]` + `IAuthorizationRequirementData`. |
| `PendingRoleGrant`, `UserProvisioningState`, `GET /api/v1/me` | Found in **neither** repo | n/a |

**Duplication or competing paths in wrigley-web.** The only competing path is the role-name check in `RoleService.ResolveAdministratorAsync` versus the permission check in `[RequirePermission]`. Both are deny-by-default. If both stay, an endpoint's admin semantics depend on which path it was built with. The frontend adds a third interim gate: `classification === "Internal"`, per `frontend/AGENTS.md`.

## Gap checklist

| Gap | Status | Evidence | Task # |
| --- | --- | --- | --- |
| Admin.Access granted to some role | Missing | No `role_permissions` seed; `RoleConfiguration.cs:76-85` | 1 |
| A platform-scoped admin role + assignment | Missing | Only org-scoped grant at `CreateOrganizationHandler.cs:64`; `CreatePlatformRole` used only in tests | 1 |
| `GET /api/roles` | Missing | `RolesController.cs` has no GET | 4 |
| `GET /api/roles/{id}` | Missing | same | 4 |
| `GET /api/roles/{id}/permissions` | Missing | same; frontend works around it with `frontend/src/app/lib/role-registry.ts` | 4 |
| `GET /api/users` | Missing | `UsersController.cs` routes only `me` | 5 |
| `GET /api/users/{id}` | Missing | same | 5 |
| `GET /api/users/me` incl. roles/permissions | Partial | Endpoint exists; `CurrentUserDto.cs:4` has no roles/permissions | 5 |
| `POST /api/users/{id}/roles` | Missing | no route; `Permissions.RoleAssign` is unused outside the catalog | 7 |
| `DELETE /api/users/{id}/roles/{roleId}` | Missing | no route; no `UserRole` removal anywhere | 7 |
| `POST /api/users/{id}/disable` / `enable` | Partial | Domain `User.Disable/Enable` exist (`User.cs:57,63`); no handler, repository method or route | 8 |
| `GET /api/organizations/{id}` | Missing | `OrganizationsController.cs` has only POST and GET-mine | 6 |
| `PUT /api/organizations/{id}` | Missing | no route; `Organization` has no rename/update method (only `Disable`, `Organization.cs:55`) | 9 |
| `GET /api/organizations/{id}/members` | Missing | no route | 6 |
| `POST /api/organizations/{id}/members/{userId}` | Partial | `IOrganizationRepository.AddMembership` exists (`IOrganizationRepository.cs:12`); no route. Re-adding hits the unique (user, org) index, and `OrganizationMembership` has no re-enable method | 9 |
| `DELETE /api/organizations/{id}/members/{userId}` | Partial | `OrganizationMembership.Disable` exists (`OrganizationMembership.cs:39`); no route | 9 |
| `IUserRepository.ListAsync` / `GetByIdAsync` / `UpdateStatusAsync` | Missing | `IUserRepository.cs` has only `InsertIfAbsentAsync`, `GetStatusByUidAsync`, `GetIdByUidAsync` | 3 |
| `IRoleRepository.ListRolesAsync` / `GetRolePermissionsAsync` / `ListUserRolesAsync` / `RemoveUserRole` | Missing (`AddUserRole` exists) | `IRoleRepository.cs:10-33` | 3 |
| `IOrganizationRepository.ListAsync` / `GetByIdAsync` / `Update` / `ListMembersAsync` / `RemoveMembership` | Missing | `IOrganizationRepository.cs` has only `Add`, `AddMembership` | 3 |
| `IAuthorizationQueries.ListUsersAsync` / `ListMembersAsync` / `ListRolesAsync` | Missing | `IAuthorizationQueries.cs:8-27` | 3 |
| `RoleDto` + `OrganizationId` | Missing | `RoleContracts.cs:17` is `RoleDto(int Id, string Name, string Description)` | 2 |
| `CurrentUserDto` + roles/permissions | Missing | `CurrentUserDto.cs:4` | 2 |
| `UserSummaryDto`, `UserDetailDto`, `RoleDetailDto`, `OrganizationMemberDto`, assignment and status request DTOs | Missing | not found in `…Application` or `…Api` | 2 |
| Audit types `user_disabled`, `user_enabled`, `role_assigned`, `role_revoked`, `organization_updated`, `membership_added`, `membership_removed` | Missing | `IdentityAuditEventTypes.cs` has 10 others, none of these | 2 |
| Controllers use `[RequirePermission]` | Missing | only `tests/…/Fixtures/ProbeController.cs:26,31,35` | 10 |
| Org-scoped routes carry `{organizationId}` | Missing | no production route has it; `ADMIN-API-GAPS.md` proposes `{id}` (would bypass the guard) | 10 |
| Platform vs org admin decided; first admin bootstrapped | Missing | see Decisions needed | 1, 10 |
| Reuse primitives (`[RequirePermission]`, resolver, gates, `RoleService` pattern, `ProblemResults`, unit of work, `IAuditLog`, `PermissionScope`, `IdentitySeed`) | Already done | files cited in Architecture map and Authorization pipeline | n/a |

## Test baseline

Run on 2026-10-04 with `dotnet test ScrapGo.Core.slnx --no-build` (Docker 28.5.1). **127 passed, 0 failed, 0 skipped.**

| Project | Passed | Failed | Duration |
| --- | --- | --- | --- |
| `ScrapGo.Core.Modules.Identity.IntegrationTests` | 97 | 0 | 10 min 9 s |
| `ScrapGo.Core.Modules.QuickbaseEngine.IntegrationTests` | 30 | 0 | 27 s |

**Setup** (`tests/Modules/ScrapGo.Core.Modules.Identity.IntegrationTests/Fixtures/`):

- **Database:** `IdentitySpecFixture` starts a fresh Testcontainers `postgres:16-alpine` per scenario class (`IClassFixture`). It applies the `AuditDbContext` then `IdentityDbContext` migrations, and exposes `DbContext` for arranging and asserting. Tests run serially (`AssemblyInfo.cs`: `DisableTestParallelization`), because configuration is passed through environment variables.
- **Auth is not mocked.** The fixture boots the real host via `WebApplicationFactory<Program>`, swaps the GCIP JWKS HTTP client for an in-memory handler serving the fixture's RSA public key, and mints real RS256 tokens with `CreateToken(uid, email, hd, authTime, signInProvider, …, extraClaims)`. So the whole authentication and authorization pipeline runs.
- **`IdentitySeed` helpers:** `SeedUserAsync`, `SeedOrganizationAsync(disabled)`, `SeedMembershipAsync(disabled)`, `SeedMemberAsync`, `SeedOrganizationAdministratorAsync`, `GrantPermissionsAsync(userId, organizationId?, permissions…)` (`organizationId` null gives a platform role and assignment), and `AssignRoleAsync`.
- **`ProbeController`:** test-only routes registered as an application part. It's the only place `[RequirePermission]` (organization-scoped, missing-organization and platform-scoped) is exercised.

**Templates:**
- `Roles/CustomRoleCrud.cs`: admin, non-admin, cross-org and duplicate scenarios, `PostRoleAsync` / `ReadIdAsync` helpers.
- `Organizations/CrossTenantIdorGuard.cs`: the `{organizationId}` guard, per HTTP verb, plus its audit row.
- `Organizations/CreateOrganization.cs`: a simpler create flow.

## Decisions needed

1. **Platform admin vs organization admin.** Recommendation: **both, with distinct roles and route shapes.**
   - *Platform admin* (ScrapGo staff) uses `[RequirePermission(X, PlatformScope = true)]` on cross-organization routes:
     - `GET /api/users`, `GET /api/users/{id}`
     - user enable and disable
     - an all-organizations list (a new route such as `GET /api/admin/organizations`, because `GET /api/organizations` already means "mine")
     - platform role assignment

     Gate on specific permissions (`User.Read`, `User.Update`, `Role.Assign`); keep `Admin.Access` only as "may open the admin portal".
   - *Organization admin* uses `[RequirePermission(X)]` on routes carrying `{organizationId}`:
     - `GET/PUT /api/organizations/{organizationId}`
     - `…/{organizationId}/members[/{userId}]`
     - `…/{organizationId}/roles`
     - role assignment at `…/{organizationId}/members/{userId}/roles[/{roleId}]` rather than `/api/users/{id}/roles`

     This puts the membership guard and organization-scoped resolution on every one of them.
2. **First platform admin bootstrap.** Recommendation:
   - **Migration:** add `DefaultRoleNames.PlatformAdministrator`, seed it as a platform role (`organization_id` null), and seed its `role_permissions`: `Admin.Access`, `User.*`, `Role.*` and `Role.Assign`.
   - **First grant:** an explicit, one-time host command (for example `dotnet ScrapGo.Core.Api.dll bootstrap-platform-admin --uid <GCIP uid>`), not an HTTP endpoint and not automatic. It requires that the user is already provisioned (via `GET /api/users/me`), and it refuses if any platform administrator already exists. In one transaction, it inserts the platform-scoped `user_roles` row and an audit row (`platform_admin_bootstrapped`, `ActorType = System`).
   - **Never** put `Admin.Access` on `OrganizationAdministrator`: any signed-in user can create an organization and become its administrator.
3. **Switch `IsOrganizationAdministratorAsync` to a permission check.** Recommendation: **yes, in task 10**, after task 1 seeds organization-level permissions (`Role.*`, `Role.Assign`, `User.Read`) onto `OrganizationAdministrator`. `RoleService` would then call `PermissionResolver` with the role's organization, rather than matching the role name. Pair it with an **escalation guard**: a caller may only attach permissions they already hold in that scope, and may not edit a role they themselves hold. Without that guard, permission-based role management lets a `Role.Update` holder grant themselves anything.
4. **Pagination and filter conventions.** There's **no existing paging type**: no `PagedResult`, `PageRequest` or `Skip/Take` in `src/`. Recommendation:
   - add `PagedResult<T>(Items, Page, PageSize, TotalCount)` to `Shared.Kernel`, with query parameters `page` (1-based, default 1) and `pageSize` (default 25, max 100), and offset paging (admin lists are small)
   - filters as explicit query parameters (`search` on email, name or slug; `status`; `organizationId`; `classification`)
   - a stable sort by id, plus an optional `sort` parameter
5. **Smaller decisions:**
   - **Membership removal and roles:** should removing a member also revoke their organization-scoped `user_roles`? Recommended yes, and audit both. The resolver doesn't check membership on its own.
   - **Re-adding a member:** re-adding a removed member should reactivate the existing row, which needs a domain method.
   - **Permission catalog:** decide ScrapGo-specific permissions before building on the generic `Invoice.*`/`Report.*` ones.
   - **`backend/AGENTS.md` layout tree** is malformed: `Shared/` and `Modules/` render under `infra/`. Fix it in task 2 or 10.

6. **Decisions raised by Task 10** (no action taken):
   - **Move the in-service org-scoped writes under `{organizationId}`?** The candidates are `POST/PUT/DELETE /api/roles…` → `/api/organizations/{organizationId}/roles…`, and `/api/users/{id}/roles` → `/api/organizations/{organizationId}/members/{userId}/roles`.
     - Moving them would add the membership guard as a second layer and make every org route uniform.
     - They are secure today (service-checked and swept), and moving them breaks the frontend's `admin-api.ts` calls.
   - **Organization creator becomes its OrganizationAdministrator automatically.** Today any signed-in user can create an organization and administer it, scoped to that organization only. If organization creation should be restricted (for example to platform admins), `POST /api/organizations` needs a permission.
   - **Disabling a user who is an organization's last OrganizationAdministrator** is allowed (only the platform-admin lock-out is blocked). The organization then has no active administrator until a platform-side fix.
   - **`GET /api/organizations/{organizationId}` and `/members` use `User.Read`.** Add an `Organization.Read` permission if organization detail should be readable without people access.

## Endpoint authorization matrix (Task 10)

Verified 2026-10-04 against every action in `src/Modules/Identity/*Api`. All controllers are `[Authorize]`. Since Task 10 the host also sets an authorization **FallbackPolicy** (authenticated user), so an action that forgets every attribute is still not anonymous. Only `/healthz` and `/healthz/ready` opt out, with `AllowAnonymous`.

**Scope**
- **platform:** `[RequirePermission(X, PlatformScope = true)]`, satisfied only by a platform-scoped assignment.
- **org (route):** `[RequirePermission(X)]` resolved against the route's `{organizationId}`.
- **org (service):** the organization comes from the body, query or stored role, and the Application service checks active membership plus the permission itself.
- **self:** the caller's own record only, no permission by design.

Test files below are in `tests/Modules/ScrapGo.Core.Modules.Identity.IntegrationTests/`.

| Route | Verb | Attribute / permission | Scope | Membership guard? | Test that proves deny |
| --- | --- | --- | --- | --- | --- |
| `/api/users/me` | GET | `[Authorize]` only | self | No | `Users/DisabledUserDenied.cs` (401, disabled 403); `Users/CurrentUserAccess.Given_a_disabled_user` |
| `/api/users/me/linked-providers/link` | POST | `[Authorize]` only; re-auth token must match the caller | self | No | `Users/SecureAccountLinking.cs` (mismatch / stale / invalid) |
| `/api/users` | GET | `User.Read` | platform | No | `Users/AdminUserReads.Given_an_authenticated_user_without_user_read`, `…Given_an_org_admin_holding_user_read_only_in_their_org` |
| `/api/users/{id}` | GET | `User.Read` | platform | No | same as above |
| `/api/users/{id}/disable` | POST | `User.Update` | platform | No | `Users/UserStatusChanges.Given_a_caller_without_platform_user_update` |
| `/api/users/{id}/enable` | POST | `User.Update` | platform | No | same as above |
| `/api/users/{id}/roles` | POST | `Role.Assign` (+ `Admin.Access` for platform scope), in `UserRoleAssignmentService` | org (service) or platform | No: org from body | `Users/UserRoleAssignment.Given_a_member_without_role_assign`, `…Given_an_org_admin_targeting_platform_scope`; `Organizations/CrossTenantSweep` |
| `/api/users/{id}/roles/{roleId}` | DELETE | same as above, org from `?organizationId=` | org (service) or platform | No: org from query | same as above |
| `/api/organizations` | POST | `[Authorize]` only: any user may create an org and becomes its admin | self | No (by design: how a first membership is made) | `Organizations/CreateOrganization.cs` |
| `/api/organizations` | GET | `[Authorize]` only: the caller's own orgs | self | No | `Organizations/ListMyOrganizations.cs` |
| `/api/organizations/{organizationId}` | GET | `User.Read` | org (route) | **Yes** | `Organizations/OrganizationReads.Given_a_member_without_user_read`, `…Given_an_admin_of_org_a_reading_org_b`; `CrossTenantSweep` |
| `/api/organizations/{organizationId}` | PUT | `Organization.Update` | org (route) | **Yes** | `Organizations/OrganizationAdministration.Given_a_member_without_the_permissions`; `CrossTenantSweep` |
| `/api/organizations/{organizationId}/members` | GET | `User.Read` | org (route) | **Yes** | `OrganizationReads`; `CrossTenantSweep` |
| `/api/organizations/{organizationId}/members/{userId}` | POST | `User.Update` | org (route) | **Yes** | `OrganizationAdministration.Given_a_member_without_the_permissions`; `CrossTenantSweep` |
| `/api/organizations/{organizationId}/members/{userId}` | DELETE | `User.Update` | org (route) | **Yes** | same as above |
| `/api/organizations/{organizationId}/roles` | GET | `Role.Read` | org (route) | **Yes** | `Roles/OrganizationRoleReads.Given_a_member_without_role_read`; `CrossTenantSweep` |
| `/api/organizations/{organizationId}/roles/{id}` | GET | `Role.Read` | org (route) | **Yes** | same, plus 404 for another org's role |
| `/api/organizations/{organizationId}/roles/{id}/permissions` | GET | `Role.Read` | org (route) | **Yes** | same as above |
| `/api/roles` | POST | `Role.Create`, in `RoleService` | org (service) | No: org from body | `Roles/CustomRoleCrud.Given_a_non_admin_member`, `…Given_an_admin_of_org_a_targeting_org_b`; `CrossTenantSweep` |
| `/api/roles/{id}` | PUT | `Role.Update` + escalation guard, in `RoleService` | org (service) | No: org from stored role | `CustomRoleCrud`; `CrossTenantSweep` |
| `/api/roles/{id}` | DELETE | `Role.Delete` + escalation guard, in `RoleService` | org (service) | No: org from stored role | `CustomRoleCrud`; `CrossTenantSweep` |
| `/api/roles/{id}/permissions` | POST | `Role.Update` + escalation guard, in `RoleService` | org (service) | No: org from stored role | `Roles/ComposeRoleFromPermissionSubset.cs`; `CrossTenantSweep` |
| `/api/roles/{id}/permissions/{name}` | DELETE | `Role.Update` + escalation guard, in `RoleService` | org (service) | No: org from stored role | same as above |
| `/api/permissions` | GET | `[Authorize]` only: the read-only catalog | none | No | `Roles/PermissionCatalogSeed.cs` (no write routes exist) |

**Flags**
- **No `[RequirePermission]` (authenticated only), all intentional:**
  - `GET /api/users/me` and `POST …/link`: self-service.
  - `POST` and `GET /api/organizations`: self-service; creating an organization is how a first membership is made.
  - `GET /api/permissions`: the public, read-only catalog.

  None returns another user's or another organization's data.
- **Org-scoped but no `{organizationId}` in the route:** the five `/api/roles…` writes and the two `/api/users/{id}/roles` actions.
  - They do **not** rely on the fallback policy. `RoleService` and `UserRoleAssignmentService` check active membership and the permission in the role's (or requested) organization themselves.
  - The cross-tenant sweep covers every one of them.
  - They were left in place because the frontend calls `/api/roles` (`frontend/src/app/lib/admin-api.ts`), and Task 7 specified `/api/users/{id}/roles`. Moving them is listed under Decisions needed.
- **Fixed in Task 10:** there was no FallbackPolicy (`AddAuthorization()` with defaults). Deny-by-default rested entirely on every controller remembering `[Authorize]`. Fixed in `Composition/ScrapGoModulesServiceCollectionExtensions.cs`, and proved by `Authorization/DenyByDefault.cs` with a probe action that has no attribute.

## Deny-by-default audit (Task 10)

Every code path that creates a `user_roles` or `role_permissions` row in `src/`:

| Path | What it grants | Verdict |
| --- | --- | --- |
| `BootstrapPlatformAdministratorHandler.cs:74` | PlatformAdministrator at platform scope | The documented one-time bootstrap: CLI only, refuses once any admin exists, audited. **Kept.** |
| `CreateOrganizationHandler.cs:64` | OrganizationAdministrator, scoped to the **new** organization only, to its creator | Explicit user action, documented in `backend/AGENTS.md` ("Granted only per organization, to whoever creates it"). It grants nothing in any other org or at platform scope. **Kept, flagged:** see Decisions needed. |
| `UserRoleAssignmentService.cs:230` | The requested role, to the requested user | Explicit admin action with permission checks and the escalation guard. Not automatic. |
| `RoleService.cs:199` | A permission onto a custom role | Explicit admin action with the escalation guard. Not automatic. |
| Migrations `AddPlatformAdministratorAndRolePermissions`, `AddOrganizationUpdatePermission` | Permissions onto the built-in **roles** (not users) | Seed data. Grants nothing to any user by itself. |

No grant happens on sign-in (`ProvisionCurrentUserHandler` only inserts the user row), on membership add (`OrganizationAdminService.AddMemberAsync` adds no role; `DenyByDefault.Given_a_user_who_signed_in_and_was_added_to_an_org` proves it), or on user creation. Nothing was removed.

## Write-path consistency (Task 10)

Every write endpoint commits through `IUnitOfWork.ExecuteInTransactionAsync` with its audit row staged in the same transaction:

| Handler | Audit event(s) |
| --- | --- |
| `ProvisionCurrentUserHandler` | `user_provisioned` |
| `LinkProviderHandler` | `provider_linked` |
| `CreateOrganizationHandler` | `organization_created` |
| `RoleService` (create, update, delete, attach, detach) | `role_created`, `role_updated`, `role_deleted`, `role_permission_attached` / `_detached` |
| `UserRoleAssignmentService` | `role_assigned`, `role_revoked` |
| `UserStatusService` | `user_disabled`, `user_enabled` |
| `OrganizationAdminService` | `organization_updated`, `membership_added`, `membership_removed` (+ `role_revoked` per removed role) |
| `BootstrapPlatformAdministratorHandler` (CLI) | `platform_administrator_bootstrapped` |

- **Fixed in Task 10:** `RoleService` and `LinkProviderHandler` used a bare `SaveChangesAsync`. That was already atomic (one SaveChanges is one transaction), but it was inconsistent. Both now commit through `ExecuteInTransactionAsync`.
- **Audit types:** all seven listed in `backend/ADMIN-API-GAPS.md` ("Missing audit events") exist in `IdentityAuditEventTypes` and are written: `user_disabled`, `user_enabled`, `role_assigned`, `role_revoked`, `organization_updated`, `membership_added`, `membership_removed`. None is missing.
- **Idempotent no-ops** write neither a row nor an audit entry: re-assign, re-add member, re-disable, same-name rename, revoke or remove of something not held.

## Progress

- [x] 1. Bootstrap (platform admin role + permissions seed + one-time assignment)
  - Done 2026-10-04. The `AddPlatformAdministratorAndRolePermissions` migration does two things:
    - adds `PlatformAdministrator` with User.*, Role.* and Admin.Access
    - seeds `OrganizationAdministrator` with every permission except Admin.Access
  - First admin is granted by the `bootstrap-platform-admin --uid` host command (Granted / AlreadyGranted / UserNotProvisioned / AnotherAdministratorExists, audited as `platform_administrator_bootstrapped`).
  - Suite: 143 passed, 0 failed (Identity 113, QuickbaseEngine 30).
- [x] 2. Contracts (DTO extensions, new DTOs, audit event types)
  - Done 2026-10-04. Contracts only, no endpoints or handlers.
    - `RoleDto` gains `OrganizationId` (null = built-in platform role).
    - `CurrentUserDto` gains `Roles` and `Permissions` (one `ScopedPermissionsDto` per scope, never flattened). They return empty until Task 5.
    - New: `AssignedRoleDto`, `UserSummaryDto`, `UserDetailDto`, `RoleDetailDto`, `OrganizationMemberDto`, `AssignRoleRequest`, `UpdateOrganizationRequest`, `PagedResult<T>` and `PageRequest` (Shared.Kernel; page 1-based, default 25, max 100).
    - Validation follows the existing pattern: nullable request records, plus an `IsWellFormed` / `IsValid` check on `AssignRoleCommand`, `UpdateOrganizationCommand` and `PageRequest` that handlers map to 400 `invalid_request`.
    - The seven missing audit event types were added.
  - No `UpdateUserStatusRequest`: enable/disable are separate routes with no body.
  - `DisplayName` is in the DTOs but always null: `User` stores no display name yet.
  - Suite: 168 passed (Identity 138, QuickbaseEngine 30).
- [x] 3. Repositories (user, role, organization, authorization queries)
  - Done 2026-10-04. Split by the existing convention:
    - List reads that project to DTOs live in `IAuthorizationQueries` (like `ListActiveOrganizationsAsync`).
    - Repositories hold tracked entities and per-aggregate reads.
  - `IAuthorizationQueries`:
    - `ListUsersAsync(UserListFilter, PageRequest)`: email ILIKE search with wildcards escaped, status filter.
    - `ListOrganizationsAsync(OrganizationListFilter, PageRequest)`: name/slug search, status filter. Replaces `IOrganizationRepository.ListAsync`.
    - `ListMembersAsync(orgId, PageRequest)`: active memberships only. Roles are filtered to the same organization at query level.
    - `ListRolesAsync(orgId)`: that organization's active roles plus the built-ins, never another organization's.
  - `IUserRepository`: `GetByIdAsync` (tracked), `UpdateStatusAsync` (domain Enable/Disable on the tracked entity, returns the previous status).
  - `IRoleRepository`: `GetRolePermissionsAsync`, `ListUserRolesAsync` (all scopes, platform first), `FindUserRoleAsync`, `RemoveUserRole`.
  - `IOrganizationRepository`: `GetByIdAsync` (tracked), `FindMembershipAsync`, `RemoveMembership` (hard delete).
  - Skipped as duplicates of the query methods: `IUserRepository.ListAsync`, `IRoleRepository.ListRolesAsync`, `IOrganizationRepository.ListMembersAsync`.
  - Skipped `IOrganizationRepository.Update`: tracked entities need no Update call, and `Organization` has no rename method yet (Task 9).
  - No writes open transactions. Tests run them inside `ExecuteInTransactionAsync`.
  - No new migration: every list filter and join already has an index. Contains-search can't use a btree index, which is acceptable at admin-list scale; add a `pg_trgm` GIN index if the users table grows large.
  - Suite: 186 passed (Identity 156, QuickbaseEngine 30).
- [x] 4. Roles read (`GET /api/roles`, `/{id}`, `/{id}/permissions`)
  - Done 2026-10-04 at **`/api/organizations/{organizationId}/roles`**, `/{id}` and `/{id}/permissions`, per Decision 1. The route change is noted in `backend/ADMIN-API-GAPS.md`.
  - Each action has `[RequirePermission(Role.Read)]`, org-scoped, and the membership guard covers the route.
  - `RoleReadService` (Application) maps `RoleReadOutcome.Success/NotFound` in `OrganizationRolesController`.
  - The list returns that organization's active roles plus the built-ins (`organizationId` null).
  - Another organization's role, a soft-deleted role or an unknown id is 404 (`IAuthorizationQueries.FindRoleAsync`, the same visibility rule as the list).
  - Swagger documents all three routes (asserted by a spec).
  - Suite: 205 passed (Identity 175, QuickbaseEngine 30).
- [x] 5. Users read + `/me` (list, detail, roles and permissions on `/me`)
  - Done 2026-10-04.
  - `GET /api/users` (`search` on email, `status`, `page`, `pageSize`; 400 `invalid_request` on a bad value) and `GET /api/users/{id}` (`UserDetailDto`, roles in every scope with `organizationId`).
    - Both have `[RequirePermission(User.Read, PlatformScope = true)]`, per Decision 1.
    - An org admin's org-scoped `User.Read` gets 403 here; org-level people views are Task 6's member routes.
  - `GET /api/users/me` fills `roles` and `permissions` (one entry per scope) from `PermissionResolver.GetEffectiveAccessAsync`. That uses the same cached per-scope resolution as `HasPermissionAsync`.
    - Organization scopes appear only with an active membership, mirroring the membership guard.
    - `/me` stays open to any authenticated, non-disabled caller.
  - `GET /api/v1/me` does not exist in this repo (re-checked), so nothing needs retiring.
  - Suite: 226 passed (Identity 196, QuickbaseEngine 30).
- [x] 6. Organization read (detail, members)
  - Done 2026-10-04. `GET /api/organizations/{organizationId}` returns `OrganizationDetailDto` (id, name, slug, status, createdAt, activeMemberCount). `GET /api/organizations/{organizationId}/members` returns a paged `OrganizationMemberDto` list (400 `invalid_request` on a bad page).
  - Both are in `OrganizationDetailController` via `OrganizationReadService`, under the membership guard, with `[RequirePermission(User.Read)]` org-scoped.
    - No organization permission exists in the catalog, and Decision 1 named none. `User.Read` was chosen because every org admin holds it and members are a people view.
    - Revisit if an `Organization.Read` permission is added.
  - **An unknown organization id returns 403 `no_active_membership`, not 404.** The guard answers before the handler and treats it like any organization the caller isn't in, so organization ids can't be probed. The handler's own NotFound is covered by a direct service spec.
  - **Platform admin:** not decided for these routes (Decision 1 proposes a separate `GET /api/admin/organizations`). A platform administrator who isn't a member gets 403 here, asserted by a spec.
  - Suite: 241 passed (Identity 211, QuickbaseEngine 30).
- [x] 7. Role assign/revoke
  - Done 2026-10-04. `POST /api/users/{id}/roles` (body `AssignRoleRequest { roleId, organizationId? }`) and `DELETE /api/users/{id}/roles/{roleId}?organizationId=`. A missing `organizationId` means platform scope.
  - `UserRoleAssignmentService` authorizes per scope (no `{organizationId}` route value):
    - org scope: active membership + `Role.Assign` there, else 403 `missing_permission`
    - platform scope: `Admin.Access` + `Role.Assign` at platform scope, else 403 `platform_admin_required`
  - Rules, each its own outcome:
    - a role from another org: 404 `role_not_found`, deliberately the same as an unknown role
    - PlatformAdministrator per org, or OrganizationAdministrator at platform scope: 400 `role_scope_mismatch`
    - target not an active member: 400 `user_not_a_member`
    - escalation guard (the caller must hold every permission the role grants): 403 `cannot_grant_unheld_permission`
    - revoking the last *active* PlatformAdministrator (row-locked): 409 `last_platform_administrator`
  - Idempotent: assigning an already-held role returns 200 with no write and no audit (like attach-permission). Revoking an unheld role returns 204 (like detach). A concurrent duplicate insert maps to the same 200.
  - One transaction per write with `role_assigned` / `role_revoked` audit rows. Actor = `UserId`; metadata holds `targetUserId`, `roleId`, `organizationId`.
  - The target scope's permission cache is invalidated, and a spec proves the next request sees the change.
  - Suite: 257 passed (Identity 227, QuickbaseEngine 30).
- [x] 8. User enable/disable
  - Done 2026-10-04. `POST /api/users/{id}/disable` and `/enable` have `[RequirePermission(User.Update, PlatformScope = true)]` (Decision 1). Org-scoped `User.Update` gets 403.
  - `UserStatusService` wraps `User.Disable/Enable`. Each change is one transaction with its `user_disabled` / `user_enabled` audit row (actor = `UserId`, metadata `targetUserId`).
  - Rules:
    - self-disable: 403 `cannot_disable_self`
    - disabling the last *active* PlatformAdministrator (row-locked, the same lock as revoke): 409 `last_platform_administrator`
    - already in that state: 204 with no write or audit
  - Immediate effect: `DisabledUserGateMiddleware` reads status from the database on every request with no cache. A spec proves the very next request is 403 `user_disabled`.
  - No Identity Platform `disabled` custom claim exists (none in code or this audit), so none was added or updated.
  - Task 7's revoke now also counts only *active* other PlatformAdministrators.
  - Suite: 270 passed (Identity 240, QuickbaseEngine 30).
- [x] 9. Organization update + membership add/remove
  - Done 2026-10-04.
    - `PUT /api/organizations/{organizationId}` renames and returns `OrganizationDetailDto`. The slug is kept; 400 `invalid_name`. It is gated on the new `Organization.Update` permission (catalog id 18, appended), seeded onto OrganizationAdministrator by `20261005044925_AddOrganizationUpdatePermission` (the user chose this on 2026-10-04).
    - `POST` / `DELETE /api/organizations/{organizationId}/members/{userId}` need `User.Update` in that organization. All three routes sit under the membership guard.
  - `OrganizationAdminService` runs each write in one transaction with its audit rows:
    - `organization_updated` (before/after name)
    - `membership_added` (with `reactivated`)
    - `membership_removed`, plus one `role_revoked` (`reason: membership_removed`) per role the member held in that organization
  - Rules:
    - Adding grants no roles.
    - Re-adding an active member returns 204 with no audit; a disabled membership is reactivated (`OrganizationMembership.Enable`).
    - An unknown user is 404. An unknown organization is 403 from the guard, as in Task 6.
    - Removing a non-member returns 204 as a no-op.
    - Removing the last *active* OrganizationAdministrator (row-locked) is 409 `last_organization_administrator`. Task 7's revoke now enforces the same rule.
  - Removal invalidates the member's cached permissions for that organization.
  - Suite: 289 passed (Identity 259, QuickbaseEngine 30).
- [x] 10. Authorization sweep (`[RequirePermission]` everywhere, `{organizationId}` routes, `RoleService` to permission checks + escalation guard)
  - Done early in Task 1:
    - `RoleService` now checks Role.Create / Role.Update / Role.Delete through `PermissionResolver`, and `IsOrganizationAdministratorAsync` was removed.
    - Escalation guard: `cannot_grant_unheld_permission`, `cannot_modify_own_role`.
  - Sweep done 2026-10-05. See "Endpoint authorization matrix", "Deny-by-default audit" and "Write-path consistency" above.
    - **Fixed:** added an authenticated FallbackPolicy, with the health checks explicitly `AllowAnonymous`.
    - **Fixed:** `RoleService` and `LinkProviderHandler` now commit through `ExecuteInTransactionAsync`.
    - **Added:** `Organizations/CrossTenantSweep.cs`, 18 cases covering every org-scoped endpoint, called by org A's admin against org B's ids. Each asserts 403/404, no org B names or emails in the body, and org B's rows unchanged.
    - **Added:** `Authorization/DenyByDefault.cs`.
    - **Left as decisions:** the in-service org-scoped routes, auto-admin on org creation, last-org-admin on user disable, and `Organization.Read` (Decision 6).
  - **Final suite: 312 passed, 0 failed (Identity 282, QuickbaseEngine 30), against the Task 0 baseline of 127 (Identity 97, QuickbaseEngine 30).**
