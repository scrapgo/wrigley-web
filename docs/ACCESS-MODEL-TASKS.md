\# Access Model V1 — Task List for Claude Code



Small, ordered tasks that remove the organization/application/module model and build the model in \[`ACCESS-MODEL.md`](ACCESS-MODEL.md).



\## How to use this



1\. Put this file and `ACCESS-MODEL.md` in `docs/` on a new branch: `git checkout -b SG-access-model-v1`.

2\. In VS Code, open the Claude Code panel and send one task at a time:



&#x20;  > Do task \*\*B3\*\* in `docs/ACCESS-MODEL-TASKS.md`.



3\. Review the diff, run the app if the task touches a screen, then let it commit. Tick the box below.

4\. Start a new Claude Code conversation every few tasks so it doesn't carry stale context.



Do them in order. Backend (B) and frontend (F) can run in parallel after \*\*B11\*\*, since the frontend needs `/api/session` and `/api/me`.



\## Rules for every task



Claude Code must follow these on every task, without being told again:



\- Read `AGENTS.md`, `backend/AGENTS.md` or `frontend/AGENTS.md` (whichever applies) and `docs/ACCESS-MODEL.md` first. Where they disagree, `ACCESS-MODEL.md` wins on the model and `AGENTS.md` wins on code conventions.

\- Do only this task. Don't start the next one or refactor unrelated code.

\- No role-name checks in code. Check permission keys from `Permissions.cs`.

\- Every authorization rule touched gets an ALLOW test and a DENY test.

\- Finish with a green build and tests:

&#x20; - backend: `dotnet build backend/ScrapGo.Core.slnx` and `dotnet test backend/ScrapGo.Core.slnx`

&#x20; - frontend: `npm run build` and `npm run lint` in `frontend/`

\- Then make \*\*one commit\*\* named `<task id>: <title>`, and reply with a 3-line summary and anything left for a later task.



\---



\## Backend



\### Phase 1 — Clear out the old model



\- \[x] \*\*B1 · Accept the allow-list as an array or a string\*\*

&#x20; > In the Identity module, change how `INTERNAL\_HD\_ALLOWLIST` is read so it accepts a JSON array in appsettings (`"INTERNAL\_HD\_ALLOWLIST": \["scrapgo.com"]`) and the comma-separated string form used by the Cloud Run env var. Today `configuration\[key]` returns null for an array, which silently makes everyone External. Read the children of the section when there are any, otherwise parse the string. Set `appsettings.Development.json` to `\["scrapgo.com"]`. Add tests for: array, comma string, blank, and mixed case.

&#x20; >

&#x20; > \*\*Done when\*\* both forms classify `scrapgo.com` as Internal in a test.



\- \[x] \*\*B2 · Remove applications, modules and the catalog\*\*

&#x20; > Delete the application catalog, organization applications, modules, `DownstreamApplication` and everything that serves them: `CatalogController`, `OrganizationApplicationsController`, `PlatformApplicationsController`, `ApplicationAccessService`, `ApplicationCatalogService`, `OrganizationApplicationService`, `ApplicationContracts`, the `Domain/Applications` folder, their EF configurations and repository, and the matching tests under `IntegrationTests/Applications`.

&#x20; >

&#x20; > Delete the org/app-scoped `SuppliersController`, keeping `PlatformSuppliersController`. Remove `HasApplicationPermissionAsync` from `IUserContext` and its callers. Temporarily make `SupplierService` check only the platform permission it already uses for `/api/suppliers`.

&#x20; >

&#x20; > Don't touch migrations yet; B5 resets them. Remove the DbSets so the model compiles, and skip any test that only fails because of the schema, with `\[Fact(Skip = "B5 resets schema")]`.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green and no `Application`, `Module` or `Catalog` types remain in the Identity module.



\- \[x] \*\*B3 · Remove organizations, memberships and org invitations\*\*

&#x20; > Delete organizations, memberships and organization invitations: `Organizations` controllers and services, `OrganizationMembershipGuardMiddleware`, `OrganizationContextAuthorizationResultHandler`, `OrganizationRouteValues`, `\[PlatformAdministration]`, the `Domain/Organizations` folder, `OrganizationRolesController`, their EF configurations and repositories, and the matching tests (Organizations, the cross-tenant tests, `PlatformAdministrationRoutes`).

&#x20; >

&#x20; > Make roles flat: no `organization\_id` or `application\_id` in code. Keep `PlatformAdministrator` as the only role for now. B4–B9 replace the rest.

&#x20; >

&#x20; > Remove the middleware registrations from the hosting pipeline. Skip schema-dependent tests as in B2.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green and nothing references `OrganizationId` in the backend.



\- \[x] \*\*B4 · Remove account linking\*\*

&#x20; > Delete `LinkedCredential`, `LinkedCredentialStatus`, `LinkProviderHandler`, the `LinkProvider\*` request and response types, the link endpoint on `UsersController`, `ILinkedCredentialRepository` and its implementation, `IReauthTokenValidator` if nothing else uses it, and `SecureAccountLinking` tests. GCIP's "one account per email" setting replaces this.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\### Phase 2 — New schema



\- \[x] \*\*B5 · New domain entities and EF configuration\*\*

&#x20; > Build the entities and EF configurations for every table in section 5 of `ACCESS-MODEL.md`, in the Identity module:

&#x20; > - `User`: rename `Classification` to `UserType`; add `DisplayName`, `SignInProvider`, `LastSignInAt`.

&#x20; > - New: `Role`, `UserRole`, `Feature` with `FeatureState`, `FeatureTarget`, `Permission` with `PermissionAudience`, `RolePermission`, `UserSupplier`, `Invitation` with `InvitationStatus`.

&#x20; > - Delete the old `Role`, `UserRole`, `RolePermission`, `RoleStatus`, `Permission` types they replace.

&#x20; >

&#x20; > Follow repo conventions: int keys, text enums with named CHECK constraints, and fluent configuration. Add the composite FKs `(user\_id, user\_type)` and `(role\_id, user\_type)`, the CHECKs, the partial unique index on pending invitation email, and NULLS NOT DISTINCT on `feature\_targets`.

&#x20; >

&#x20; > Don't generate a migration in this task.

&#x20; >

&#x20; > \*\*Done when\*\* the solution builds.



\- \[x] \*\*B6 · Reset Identity migrations and seed\*\* (done as B6-alt: forward migration `AccessModelV1`, users kept)

&#x20; > Delete every file in the Identity `Persistence/Migrations` folder. Generate one migration, `InitialAccessModel`, from the new model. Seed the three roles, the features, the permissions and the role-permission matrix exactly as in section 6 of `ACCESS-MODEL.md`, using the constants from `Permissions.cs`, which you create in Domain.

&#x20; >

&#x20; > Add integration tests (Testcontainers) that prove:

&#x20; > - an external user can't hold `site\_admin` (the DB rejects it);

&#x20; > - an internal user can't get a `user\_suppliers` row;

&#x20; > - a locked feature can't be set `Off`;

&#x20; > - the seeded matrix matches the spec.

&#x20; >

&#x20; > Un-skip or delete the tests skipped in B2/B3.

&#x20; >

&#x20; > Add a `docs/ACCESS-MODEL.md` note under "Deploying": the `identity` schema must be dropped once per environment before this migration runs.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



&#x20; > ⚠️ \*\*B6-alt\*\*, only if prod has real users to keep: instead of deleting migrations, write a forward migration that drops the old tables, keeps `users` (mapping `classification` → `user\_type`), and creates the new ones.



\### Phase 3 — Authorization engine



\- \[x] \*\*B7 · Access context provider and cache\*\*

&#x20; > Create `AccessContext` (section 8), `IAccessContextProvider` and `IAccessCache` in Application, and their implementations in Infrastructure: one EF query, the access rule from section 4, an `IMemoryCache` with a 30-second TTL keyed by UID, and `EvictUser(int userId)` / `EvictAll()`.

&#x20; >

&#x20; > Add a request-scoped `ICurrentAccess` that loads it at most once per request.

&#x20; >

&#x20; > Add tests for: an active user with a granted permission; a disabled user; a feature `Off`; `Targeted` with a user target; `Targeted` with a role target; `Targeted` with no match; and eviction.

&#x20; >

&#x20; > \*\*Done when\*\* the tests are green.



\- \[x] \*\*B8 · Permission policies replace `\[RequirePermission]`\*\*

&#x20; > Replace `PermissionResolver`, `AuthorizationQueries`, `PermissionAuthorizationHandler`, `PassThroughPermissionCache` and `IPermissionCache` with:

&#x20; > - a `PermissionPolicyProvider` that builds `perm:` policies, where `a|b` means any of;

&#x20; > - a `PermissionHandler` that uses `ICurrentAccess`;

&#x20; > - a `\[RequirePermission(params string\[] anyOf)]` attribute for controllers.

&#x20; >

&#x20; > Add an `IAuthorizationMiddlewareResultHandler` that returns 403 ProblemDetails (`reason: missing\_permission`) and writes an `access.denied` audit row. Keep the deny-by-default FallbackPolicy. Update `DisabledUserGateMiddleware` to use `ICurrentAccess`.

&#x20; >

&#x20; > Port `DenyByDefault` and `RequirePermission` tests to the new engine using the test `ProbeController`.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green and `PermissionResolver` is gone.



\- \[x] \*\*B9 · Slim `IUserContext` for other modules\*\*

&#x20; > In Shared.Kernel, reduce `IUserContext` to `IsAuthenticated`, `IdentityPlatformUid`, `IsActiveUserAsync`, `HasPermissionAsync(string permission, CancellationToken)` and `GetSupplierScopeAsync(CancellationToken)`. The scope returns `All` or a set of Record IDs: `All` when the caller has `suppliers.view\_all`, their linked IDs when they have `suppliers.view\_own`, and none otherwise. Implement it in Identity on top of `ICurrentAccess`. Update `QuickbaseQueryService`'s gate and the Quickbase test fakes.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\### Phase 4 — Sign-in



\- \[x] \*\*B10 · `POST /api/session` and `GET /api/me`\*\*

&#x20; > Implement section 7 for the internal path and the "anything else" path:

&#x20; > - `POST /api/session` creates an Internal user with `scrapgo\_user` only when the provider is `google.com`, `email\_verified` is true and `hd` is on the allow-list. Otherwise it returns 403 `no\_access` and creates no row.

&#x20; > - Disabled users get 403 `account\_disabled`.

&#x20; > - Keep the exactly-once insert (`InsertIfAbsentAsync`).

&#x20; > - Remove auto-provisioning from `GET /api/users/me`. Replace that endpoint with `GET /api/me`, which returns `{ user, roles, permissions, features }` and 403 `no\_access` for an unknown UID.

&#x20; >

&#x20; > Port `HdAllowListClassification`, `FirstRequestAutoProvisioningIsExactlyOnce`, `DisabledUserDenied` and `ExternalUserRules`. Add tests for a password sign-in on the company domain (denied) and an unverified email (denied).

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[x] \*\*B11 · Invitations\*\*

&#x20; > Implement `POST/GET /api/admin/invitations`, `POST …/{id}/resend` and `DELETE …/{id}` (section 9), and the invitation branch of `POST /api/session`:

&#x20; > - The role must be external, with one or more supplier Record IDs.

&#x20; > - Token: 32 random bytes, base64url; store only its SHA-256; 7-day expiry.

&#x20; > - Acceptance needs the token, `email\_verified` and a matching email, and creates the user, role and supplier links in one transaction.

&#x20; >

&#x20; > Behind an `IInvitationSender` interface, ship a V1 implementation that logs, and return the accept link (`{frontendBaseUrl}/invite/accept?token=…`) in the create and resend responses so the admin can copy it. Real email is a later task.

&#x20; >

&#x20; > Tests: expired, revoked, used, wrong email, unverified email, internal role rejected, and a ScrapGo User can invite (allow) while an external user can't (deny).

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[x] \*\*B12 · `bootstrap-site-admin` command\*\*

&#x20; > Rename the `bootstrap-platform-admin` host command to `bootstrap-site-admin --uid <uid>`. It assigns `site\_admin` to an existing Internal user. It refuses External users, and refuses to run if any active Site Admin exists. Audit it. Update `PlatformAdministratorBootstrap` tests and the README.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\### Phase 5 — Suppliers and admin API



\- \[x] \*\*B13 · Supplier scoping\*\*

&#x20; > Protect every `/api/suppliers…` read with `suppliers.view\_all|suppliers.view\_own`.

&#x20; > - In `SupplierService`, use `IUserContext.GetSupplierScopeAsync`. For the list, add a Record ID filter to the Quickbase query when scoped (`{3.EX.'id'}OR…` in `SuppliersTable` terms). An empty scope returns an empty page.

&#x20; > - For `/{recordId}` and every sub-resource, return 404 `supplier\_not\_found` when the record isn't in scope, before calling Quickbase.

&#x20; > - Keep the dropdown endpoints readable by anyone with a supplier view permission.

&#x20; > - Update `SupplierPermissions` and the pinning spec.

&#x20; >

&#x20; > Tests: internal sees all; external sees only linked suppliers; external gets 404 on another supplier and on its sub-resources.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[ ] \*\*B14 · Admin users API\*\*

&#x20; > Implement `GET /api/admin/users` (search, type and status filters, paged), `GET /api/admin/users/{id}`, `POST …/disable`, `POST …/enable`, `PUT …/roles` and `PUT …/suppliers` (section 9). Each change audits and evicts the user's cached access. Enforce: no self-disable; same-type roles only; never remove the last active Site Admin (409, under a row lock); supplier links for External users only. Rewrite `UsersController` and delete what it no longer needs. Port `UserStatusChanges`, `UserRoleAssignment` and `AdminUserReads`.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[ ] \*\*B15 · Admin roles and permissions API\*\*

&#x20; > Implement `GET /api/admin/roles`, `PUT /api/admin/roles/{id}/permissions` and `GET /api/admin/permissions` (grouped by feature). Enforce the audience rule. Audit, then `EvictAll()`. No create or delete of roles in V1. Rewrite `RolesController` and `PermissionsController` and delete the old role services. Port the role tests that still apply and delete the rest.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[ ] \*\*B16 · Admin features API\*\*

&#x20; > Implement `GET /api/admin/features`, `PUT /api/admin/features/{id}` (`{ state }`; 409 `feature\_locked` for locked features) and `PUT /api/admin/features/{id}/targets` (`{ userIds, roleIds }`). Audit, then `EvictAll()`. Test: switching `supplier\_edit` to `Targeted` for one user changes that user's `/api/me` and nobody else's.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[ ] \*\*B17 · Admin audit API\*\*

&#x20; > Implement `GET /api/admin/audit` (`audit.view`) over `audit.audit\_logs`: filter by actor, action, target and date range, newest first, paged with `PageRequest`/`PagedResult`. Read-only. Add a read-only query in Shared.Infrastructure if none exists, without breaking module boundaries.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green.



\- \[ ] \*\*B18 · Backend cleanup and docs\*\*

&#x20; > Search for leftovers: `Organization`, `Application`, `Module`, `PlatformScope`, `PlatformAdministrator`, `Admin.Access`, `workspace\_sign\_in\_required`. Remove them or rename to the new model. Update `backend/AGENTS.md` ("Authorization Model" section → a summary pointing to `docs/ACCESS-MODEL.md`), `README.md`, `ScrapGo.Core.Api.http`, and the Terraform variable description for `internal\_hd\_allowlist`. Delete `ORG-APP-MODULE-MODEL.md`, `CATALOG-AND-ADMIN-GUIDE.md` and `ADMIN-API-STATUS.md`, and rewrite `PROJECT-STATUS.md` in a few lines.

&#x20; >

&#x20; > \*\*Done when\*\* build and tests are green and the grep is clean.



\---



\## Frontend



\- \[ ] \*\*F1 · Remove organization and application UI\*\*

&#x20; > Delete the organization, application and role-assignment admin UI:

&#x20; > - every `components/admin/organization-\*`, `applications-panel`, `appoint-application-admin` and `role-assignment-panel`;

&#x20; > - routes `admin\_.organizations.$organizationId.tsx` and `invitations.tsx`;

&#x20; > - the org/app parts of `admin-api.ts`, `useAdminQueries.ts`, `useAdminAccess.ts` and `settings.tsx`.

&#x20; >

&#x20; > Leave `admin.tsx` as a placeholder page that says "Admin is being rebuilt". Regenerate routes (`npm run routes`).

&#x20; >

&#x20; > \*\*Done when\*\* build and lint pass.



\- \[ ] \*\*F2 · Keep users signed in past one hour\*\*

&#x20; > The app stores the raw GCIP ID token in `localStorage` and never refreshes it, so sessions break after an hour. Store the refresh token returned by `signInWithPassword` and `signInWithIdp`. Refresh it through `https://securetoken.googleapis.com/v1/token?key=…` shortly before expiry and on any 401, retry the request once, and sign out if refresh fails. Keep it in `api-client.ts` and `useAuth.ts`.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and a manual check with a forced-expired token refreshes and continues.



\- \[ ] \*\*F3 · Access context, `can()` and gated navigation\*\*

&#x20; > Replace the `CurrentUser` type with the `/api/me` shape (section 9 of the spec). Add `useAccess()` with `can(...anyOf)` and `isOn(feature)`, a `<Can anyOf={…}>` component, and a `RequirePermission` route wrapper that renders "not found". Build the app-shell menu from one `nav` list where each entry has `anyOf` permissions: Dashboard `dashboard.view`; Suppliers `suppliers.view\_all|view\_own`; Users `users.view`; Invitations `users.invite|users.view`; Roles `roles.view`; Features `features.view`; Audit log `audit.view`. Refetch `/api/me` on window focus, every 5 minutes, and after any 403.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and an external test user sees only Dashboard and Suppliers.



\- \[ ] \*\*F4 · Sign-in flow and no-access page\*\*

&#x20; > After any sign-in, call `POST /api/session` (with a pending invitation token from `sessionStorage` if present), then load `/api/me`. On 403 `no\_access` or `account\_disabled`, route to a new `/no-access` page with a sign-out button and the message "Ask ScrapGo for an invitation" or "Your account is disabled". Keep the Google button labelled "ScrapGo staff — sign in with Google" plus email/password.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and an unknown Gmail user lands on `/no-access`.



\- \[ ] \*\*F5 · Accept-invitation page\*\*

&#x20; > Add `/invite/accept?token=…`: store the token in `sessionStorage`, then offer "Create a password" (GCIP `signUp`, then send the verification email and ask the user to verify and return) or "Continue with Google". After sign-in, F4's flow sends the token to `POST /api/session`. Show clear errors for expired, used or mismatched invitations.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and an invited test account reaches its supplier.



\- \[ ] \*\*F6 · Admin › Users\*\*

&#x20; > Rebuild `admin.tsx` as tabs gated by permission. Users tab: a search box, type and status filters, and a paged table of name, email, type, roles, status and last sign-in. User detail (`admin\_.users.$userId.tsx`) shows role checkboxes (only roles of the user's type), supplier links (External only; add by Record ID with the supplier name looked up), and Disable/Enable, using the B14 endpoints. Hide actions without `users.manage`.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and a disable is reflected after refresh.



\- \[ ] \*\*F7 · Admin › Invitations\*\*

&#x20; > Invitations tab (`users.invite|users.view`): a form for email, role (external roles only) and one or more suppliers (search by name, store the Record ID); a table of invitations with status and expiry; Resend and Revoke; and a "Copy link" button using the link returned by the API.

&#x20; >

&#x20; > \*\*Done when\*\* build passes.



\- \[ ] \*\*F8 · Admin › Roles\*\*

&#x20; > Roles tab: a grid with permissions grouped by feature down the side, the three roles across the top, and a checkbox at each crossing. Grey out cells whose audience doesn't fit the role's type. Save per role with `PUT /api/admin/roles/{id}/permissions`. Read-only without `roles.manage`. Replace the old `roles-panel.tsx` and `permissions-panel.tsx`.

&#x20; >

&#x20; > \*\*Done when\*\* build passes.



\- \[ ] \*\*F9 · Admin › Features\*\*

&#x20; > Features tab: each feature with an Off / On / Targeted control. Locked features show a lock and no control. Targeted opens a picker for users and roles. Read-only without `features.manage`.

&#x20; >

&#x20; > \*\*Done when\*\* build passes and toggling `supplier\_edit` for yourself shows or hides its UI after refetch.



\- \[ ] \*\*F10 · Admin › Audit log\*\*

&#x20; > Audit tab: a read-only paged table (time, actor, action, target, result) with filters for action, actor and date range.

&#x20; >

&#x20; > \*\*Done when\*\* build passes.



\- \[ ] \*\*F11 · Frontend cleanup\*\*

&#x20; > Search for leftovers: `organization`, `application`, `platform`, `workspaceSignInRequired`, `Admin.Access`. Remove them. Update `frontend/AGENTS.md`, `FRONTEND-IMPLEMENTATION-STATUS.md` and `.env.example` (the `VITE\_GOOGLE\_HOSTED\_DOMAIN` note now says it must stay blank because external users also sign in with Google).

&#x20; >

&#x20; > \*\*Done when\*\* build and lint pass and the grep is clean.



\---



\## Release



\- \[ ] \*\*R1 · Deploy and verify\*\* (you, not Claude Code)

&#x20; 1. Confirm the GCIP settings: Google and Email/Password providers on, one account per email, and the `beforeSignIn` blocking function registered.

&#x20; 2. In each environment, drop the `identity` schema, then run the Audit and Identity migrations (`backend/AGENTS.md`, "Common Commands").

&#x20; 3. Sign in with your scrapgo.com Google account, then run `bootstrap-site-admin --uid <your uid>`.

&#x20; 4. Smoke test:

&#x20;    - a scrapgo.com user sees all suppliers;

&#x20;    - an invited external user sees only their linked suppliers;

&#x20;    - an uninvited Gmail user lands on "no access";

&#x20;    - a disabled user is cut off within 30 seconds.

&#x20; 5. Open the PR from `SG-access-model-v1` to `main`.

