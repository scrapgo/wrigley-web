# ScrapGo Core API (Downstream Backend)

## Architectural Overview

The `ScrapGo.Core.Api` is a Modular Monolith built with **.NET 10** and **PostgreSQL**. Its primary responsibility is acting as a secure, high-performance **Identity Hub and Intelligent Proxy** for **Quickbase**.

While Quickbase remains the System of Record for core business entities (Suppliers, Loads, Pricing, Carriers, and Media), this API abstracts Quickbase behind a clean domain layer, injecting governance, role-based access control, and aggressive caching to bypass Quickbase performance limits.

## Core Design Principles

1. **Proxy & Abstraction Layer:** The API translates clean domain requests into Quickbase API calls. The frontend never talks directly to Quickbase.

2. **PostgreSQL as Infrastructure & Governance:** Postgres strictly manages internal app state: Users, Roles, Permissions, Quickbase Definitions, Access Control lists, Action History, and Query Builder Metadata/Caches.

3. **Smart Caching & Query Builder:** To combat Quickbase latency, the Query Builder service caches query metadata and results in Postgres, serving cached data unless a configurable Time-To-Live (TTL) expires.

4. **Security-First:** Stateless JWT authentication (Google Cloud Identity Platform) with organization-scoped Role-Based Access Control (RBAC) enforced before any request hits Quickbase.

## Postgres Database Scope (Stored Locally)

- **Identity & Access:** Users, Roles, Permissions, Quickbase Application Access, Quickbase Table Access.

- **Audit & Governance:** QuickBase Action History, System Audit Logs.

- **Performance:** Quickbase Query Builder Service (saved query metadata and result caches).

## Quickbase Scope (External System of Record)

- Suppliers, Loads, Load Media (Attachments), Pricing Indexes, and Carriers.

## Tech Stack

- **Framework:** .NET 10 LTS (ASP.NET Core Web API, controllers), C# 14 (the net10.0 default)

- **Database:** PostgreSQL with EF Core via `Npgsql.EntityFrameworkCore.PostgreSQL`, snake_case via `EFCore.NamingConventions`

- **External Integration:** Quickbase REST API, via the QuickbaseEngine module (`IQuickbaseQueryService` over a typed, Polly-resilient `IHttpClientFactory` client)

- **Hosting:** Google Cloud Run (container from `Dockerfile`), Cloud SQL Postgres 16, Secret Manager; infrastructure as code in `infra/terraform/`, CI/CD in `cloudbuild.yaml`

- **Authentication:** GCIP ID tokens validated as JWT Bearer (RS256, 1-hour lifetime cap)

- **Tests:** xUnit + `WebApplicationFactory<Program>` + Testcontainers Postgres

## Solution Layout

```
ScrapGo.Core.slnx
Directory.Build.props      # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
Directory.Packages.props   # central package versions (transitive pinning on)
infra/
  terraform/                                    # GCP infrastructure (Cloud Run, Cloud SQL, Secret Manager, Artifact Registry, Cloud Build)
src/
  ScrapGo.Core.Api/                             # host / composition root only: Program.cs, Composition/, appsettings, Properties/launchSettings.json
  Shared/
    ScrapGo.Core.Shared.Kernel/                 # cross-module contracts (IAuditLog<TModule>, AuditEvent). No EF.
    ScrapGo.Core.Shared.Infrastructure/         # audit schema + AuditDbContext, Postgres conventions, ProblemDetails helpers
  Modules/
    <Module>/
      ScrapGo.Core.Modules.<Module>.Domain/          # entities, enums. Zero package/project references.
      ScrapGo.Core.Modules.<Module>.Application/     # handlers, DTOs, repository/external-service interfaces
      ScrapGo.Core.Modules.<Module>.Infrastructure/  # DbContext (own schema), repositories, external clients
      ScrapGo.Core.Modules.<Module>.Api/             # controllers + middleware; references Application only
tests/
  Modules/
    ScrapGo.Core.Modules.<Module>.IntegrationTests/   # one test project per module; no per-module wrapper folder
      Fixtures/                                       # shared test infrastructure: fixtures, seed helpers, fakes, test-only controllers
      <Feature>/                                      # mirrors the module's src feature folders (e.g. Users/, Roles/, Queries/)
```

Every project, test projects included, has a `GlobalUsings.cs`, and each namespace matches its folder path.

Naming: folder name == project name == assembly name == root namespace, always prefixed `ScrapGo.Core.` (the `ScrapGo.Core.Api` product name; the frontend is `ScrapGo.Portal.Web`).

Composition: `Program.cs` only calls `AddScrapGoModules(config)`, `AddScrapGoHosting()` and `RunScrapGoAsync(args)` (in `src/ScrapGo.Core.Api/Composition/`). `RunScrapGoAsync` runs a one-shot operator command when `args` names one (`ScrapGoCommands`, e.g. `bootstrap-platform-admin`), and otherwise applies `UseScrapGoPipeline()` and serves HTTP. To add a module, add one private `Add{Module}Module` call in `ScrapGoModulesServiceCollectionExtensions`; each module keeps its own `Add{Module}Application`/`Add{Module}Infrastructure` extensions.

Migrated modules: **Identity**: GCIP auth, user provisioning, the disabled-user gate, account linking, organizations and memberships, custom roles and the permission catalog, the `[RequirePermission]` engine, and the cross-tenant membership guard.

**QuickbaseEngine**: `IQuickbaseQueryService`, cache-aside over the Quickbase REST API, backed by `quickbase.query_caches`. Its Api project has no controllers yet.

## Quickbase Query Cache (QuickbaseEngine module)

- **Always go through `IQuickbaseQueryService`**, never `IQuickbaseClient` or `HttpClient` directly from a controller. The service hashes the query (`QueryKey`: SHA-256 of the realm plus the canonical query JSON), serves a cache row younger than `Quickbase:QueryCache:Ttl` (default 15 minutes), and otherwise calls Quickbase and upserts the row (`INSERT ... ON CONFLICT`, where the newest response wins).
- **Failures are never cached.** With `Quickbase:QueryCache:ServeStaleOnError` (on by default), a failed refresh serves the expired row instead, flagged `StaleCache`.
- **UserContext gate.** `QuickbaseQueryService` checks `IUserContext` (defined in Shared.Kernel, implemented by Identity) before reading the cache or calling Quickbase. An unauthenticated or non-active caller gets `UnauthorizedAccessException`, which the host returns as 403 `access_denied`. That check is the floor. One Quickbase credential serves all users and cache rows are shared, so table-level access (`IUserContext.HasPermissionAsync`) must still be checked by the caller until the Quickbase table-access model exists.
- **Configuration:** `Quickbase__RealmHostname` and `Quickbase__UserToken` (a secret, from Secret Manager) are validated on first use, not at startup. Never read `QuickbaseOptions` from anything that runs at startup, or every host will need the credentials to boot.
- **Resilience (Polly v8, `AddStandardResilienceHandler`):** transient failures (408, 429 honoring `Retry-After`, 5xx, network errors, timeouts) are retried with exponential backoff and jitter; other 4xx responses are not. There is also a per-attempt timeout, a total timeout and a circuit breaker. Tune them with `Quickbase__Timeout` (per attempt), `Quickbase__MaxRetryAttempts` and `Quickbase__RetryBaseDelay` (`QuickbaseResilienceOptions`). Retries are safe only because every call on this client is a read; give any future write call a pipeline with `Retry.DisableForUnsafeHttpMethods()`.
- **Changing the canonical query shape:** bump `QueryKey.Version`, which orphans the old keys.

## Authorization Model (Identity module)

The full design, its decisions and the implementation record are in [`../ORG-APP-MODULE-MODEL.md`](../ORG-APP-MODULE-MODEL.md).

### Scopes and route conventions

- **Three scopes:**
  - **platform:** no organization
  - **organization:** `{organizationId}`
  - **application:** `{organizationId}` and `{applicationId}`, within one organization

  A grant in one scope never satisfies another: not across organizations, not across applications, and not between levels.
- **Context comes from the route, never the token.**
  - Any route with `{organizationId}` is covered by `OrganizationMembershipGuardMiddleware`:
    - 403 `no_active_membership` without an active membership
    - 403 `organization_deactivated` for a member of a deactivated organization
  - Adding `{applicationId}` makes the route application-scoped: the guard answers 404 `application_not_found` unless the organization has that application, assigned and active in the catalog.
  - Name the segments exactly `organizationId` / `applicationId`: `{orgId}` or `{appId}` silently get none of this.
- **Platform administration** routes act on an organization from outside it. They live under `/api/admin/…`, carry `[PlatformAdministration]` (which skips the membership guard), and must have a platform-scoped `[RequirePermission]`. A route-table spec (`PlatformAdministrationRoutes`) fails if one doesn't.
- **Protect endpoints with permissions, not role names:** `[RequirePermission(Permissions.X)]` resolves in the route's organization, or in its application when the route has `{applicationId}`. `[RequirePermission(Permissions.X, PlatformScope = true)]` is satisfied only by platform-scoped assignments. The only role-name literals allowed are in `DefaultRoleNames`.
- **A FallbackPolicy** requires an authenticated caller on any endpoint without authorization metadata. Only `/healthz` and `/healthz/ready` are `AllowAnonymous`.

### Catalog, entitlements and resolution

- **Catalog (code, Decision 2):** applications and modules are defined in `Domain/Applications/ApplicationCatalog.cs`, and their permissions in `Permissions.All`. Both are seeded by migration. Permission names are global: `{App}.{Module}.{Action}`.
  - The catalog is **empty today** (Decision 7, blank slate).
  - To add an application, follow the steps in `ApplicationCatalog`'s remarks: definition, permissions appended, "{App} Administrator" template, migration.
  - Runtime catalog changes are status-only: retire or reactivate, with `Catalog.Manage`.
- **Entitlements (tenant data):**
  - `organization_applications`: a platform admin assigns them (`Application.Assign`).
  - `organization_application_modules`: a platform admin enables them (`Module.Manage`, licensed).
  - Assigning or enabling grants nobody anything.
- **Resolution** (`AuthorizationQueries.GetPermissionNamesAsync`): roles must be active and grants unexpired (`user_roles.expires_at`), and retired permissions never resolve. Application scope additionally requires:
  - an active membership in an active organization
  - the application assigned and active
  - each module permission's module active and enabled for that organization's application

  **A disabled module resolves to deny**, whatever the role grants.

### Roles and who grants what

- **Kinds**, by (`organization_id`, `application_id`):
  - built-ins (null, null)
  - organization custom roles (org, null)
  - application templates (null, app)
  - organization custom application roles (org, app)

  An application role holds only its own application's module permissions and `Application.ManageAccess`. Organization-level roles hold no module permissions.
- **Organization roles** (`RoleService`, `/api/roles…`, `/api/users/{id}/roles`) need an active membership plus `Role.Create` / `Role.Update` / `Role.Delete` / `Role.Assign` there. Application roles are refused on these paths (`role_scope_mismatch`).
- **Application access** (`ApplicationAccessService`, `/api/organizations/{organizationId}/applications/{applicationId}/…`) is granted **only by application administrators** (`Application.ManageAccess` at application scope) **or platform admins** (`/api/admin/…`). This is Decision 3: organization admins can't grant application roles. Grants may carry `expiresAt`.
- **Escalation guard:** a caller may only attach or grant permissions they hold in that scope (403 `cannot_grant_unheld_permission`). They may never edit, delete or recompose a role they hold (403 `cannot_modify_own_role`). A disabled module's permissions aren't held, so they can't be granted. Platform admins acting through `/api/admin/…` are exempt. Keep the guard on any new write path.
- **Last-administrator protection** (409, under a row lock): the last active PlatformAdministrator; an organization's last active OrganizationAdministrator; an application's last active administrator in an organization. The last one applies to app admins, not platform admins.
- **Built-in roles** (`DefaultRoleNames`, not editable over HTTP):
  - `OrganizationAdministrator` (id 1): every live organization-level permission (identity administration, `Organization.Update`).
  - `PlatformAdministrator`: `User.*`, `Role.*`, `Admin.Access`, plus the platform administration permissions `Organization.Create`, `Organization.Deactivate`, `Application.Assign`, `Module.Manage` and `Catalog.Manage`.
  - Retired: `Invoice.*` / `Report.*` (ids 10–16) can't be attached and never resolve. Their rows stay because ids are positional.

### Lifecycle rules

- **Deny-by-default; no role is ever assigned at sign-in.**
  - The first `PlatformAdministrator` comes only from the one-time, audited `bootstrap-platform-admin` host command.
  - Organizations are created **only by platform admins** (`POST /api/admin/organizations`), naming the first OrganizationAdministrator: an existing user id, or an email invitation.
  - Membership grants nothing.
- **Platform access needs a Google Workspace sign-in on every request** (Decision 12).
  - Platform-scoped permissions resolve only when the current token's `hd` claim is on `INTERNAL_HD_ALLOWLIST`. The check is `ICallerSignIn` in `PermissionResolver`, before the cache.
  - Otherwise `[RequirePermission(..., PlatformScope = true)]` answers 403 `workspace_sign_in_required`, in-service platform checks refuse, and `/me` shows no platform roles or permissions.
  - Organization and application access are unaffected.
  - **`INTERNAL_HD_ALLOWLIST` must be set in every environment** (Terraform default `scrapgo.com`); when it's blank, nobody has platform access.
  - GCIP tokens only carry `hd` because of the `beforeSignIn` blocking function in `infra/gcip-blocking-function/`, which copies it from Google's token for Google sign-ins. Without it deployed and registered, nobody has platform access.
- **External users** (`users.classification`, the database being the only source; Decision 8):
  - They may be organization or application administrators.
  - They may never hold a platform role (`external_user_not_allowed`, also enforced by the bootstrap command).
  - MFA for them is reserved behind `Identity:RequireMfaForExternalUsers`, off until MFA is enabled in GCIP. When it's on, a token without `firebase.sign_in_second_factor` gets 403 `mfa_required`.
- **Invitations** (`InvitationService`):
  - Pre-grants are validated at invite time like direct grants.
  - Acceptance needs the token's `email_verified` and the same email.
  - A pre-grant whose role or application is gone by then is skipped and audited.
  - Only the token's SHA-256 is stored.
- **Cascades:**
  - Removing an application hard-revokes its grants in that organization, each audited `access_revoked` (Decision 5).
  - Disabling a module keeps grants.
  - Removing a member revokes all their grants in that organization.
  - Deactivating an organization blocks everything and deletes nothing.
  - **Deleting** is platform-admin only (`DELETE /api/admin/organizations/{id}`). It works only on a deactivated organization (otherwise 409 `organization_active`), hard-deletes everything in it, and keeps its audit history plus `organization_deleted`.
  - Platform admins can also rename (`PUT /api/admin/organizations/{id}`) and set an administrator (`PUT …/administrators/{userId}`, which revokes pending invitations) without being members.
- **Permission cache:** `IPermissionCache` is a pass-through until Redis is migrated. Every mutation must invalidate the affected scopes; organization- and application-wide changes call `InvalidateOrganizationAsync` / `InvalidateOrganizationApplicationAsync`, which a real cache should implement as generation keys.

## Rules

- **Layering:** Api → Application → Domain; Infrastructure → Application. Only the `ScrapGo.Core.Api` host references Infrastructure. A module never references another module's projects. Refer to another module's data by id only (no cross-module navigations or FKs).
- **Controllers:** parse the request, call one Application handler, map the outcome. No business logic, no `DbContext`, no `HttpClient`, no repositories. Errors are RFC 7807 ProblemDetails with a `reason` extension (`ProblemResults`).
- **DTOs:** entities never cross the wire. Handlers return Application DTO records.
- **Caller context:** any module that needs "who is calling / may they do this" depends on `IUserContext` (Shared.Kernel.Security), never on Identity's projects or `HttpContext` directly.
- **External systems (Quickbase, GCIP JWKS, …):** Application interface, Infrastructure implementation using `IHttpClientFactory`. Never called from a controller.
- **Persistence:** one `DbContext` per module, own Postgres schema, own `__ef_migrations_history` table (`UseModulePostgres`). Status enums are `text` + `HasConversion<string>()` + a named CHECK constraint.
- **Audit:** stage rows with `IAuditLog<TModule>`; they commit in the module's own transaction. `AuditDbContext` owns `audit.audit_logs`; module contexts map it via `MapAuditLogs()` (excluded from their migrations).
- **C#:** primary constructors, file-scoped namespaces, a `GlobalUsings.cs` per project, pattern matching, collection expressions. `TimeProvider` for time, `IOptions<T>` for config (raw `IConfiguration` only in composition roots).

## Hosting Pipeline (`Composition/ScrapGoHostingExtensions.cs`)

The host-level setup ported from identity-platform's `Program.cs`, in pipeline order:

1. Exception handler (RFC 7807 ProblemDetails; `UnauthorizedAccessException` → 403)
2. Secure headers (`nosniff`, `no-referrer`, a deny-all CSP except on `/swagger`, HSTS outside Development)
3. Swagger UI at `/swagger` when `Swagger:Enabled` (on in Development; off by default in Cloud Run)
4. Routing, CORS allow-list (no HTTPS redirection: Cloud Run terminates TLS at its edge and the container only listens on HTTP)
5. Authentication, the Identity gates (disabled user, cross-tenant membership), authorization
6. `/healthz` (liveness, no dependencies; Cloud Run's probes) and `/healthz/ready` (Postgres checks for every module DbContext), then controllers

Not ported yet, because their modules or Redis aren't migrated: rate limiting, the audit export job, and the invitations, join-request, member-admin, platform-role, audit-log and enterprise-SSO endpoints.

The admin-portal API surface (user listing, role listing/read-back, role assignment, user status, organization/membership management) is also not migrated; see [`ADMIN-API-GAPS.md`](ADMIN-API-GAPS.md) for the full list and implementation order.

## Running Locally (against Cloud SQL, no local database)

1. `gcloud auth login` and `gcloud auth application-default login`.
2. Start the Cloud SQL Auth Proxy on port 5434: `cloud-sql-proxy <project>:<region>:<instance> --port 5434`.
3. The connection string lives in **`dotnet user-secrets`** (`UserSecretsId` `scrapgo-core-api`), never in a committed file:
   `dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5434;Database=…;Username=…;Password=…;SSL Mode=Disable" --project src/ScrapGo.Core.Api`
4. `dotnet run --project src/ScrapGo.Core.Api` → http://localhost:5141/swagger. `appsettings.Development.json` holds only non-secret values (GCIP project `wrigley-cloud-prod`, CORS for `http://localhost:3000`, Swagger on).
5. `src/ScrapGo.Core.Api/ScrapGo.Core.Api.http` has ready-made requests. Paste a GCIP ID token into `@token`.

**Secrets never go in `appsettings*.json`, `.env.example` or tfvars examples.** `.gitignore` excludes `.env*` (except `.env.example`), `*.tfvars`, Terraform state and `appsettings.*.local.json`. For `docker run --env-file`, copy `.env.example` to `.env.local`.

## Deploying (GCP)

- `infra/terraform/`: VPC, Cloud SQL (private IP for Cloud Run, plus a proxy-only public IP for developers), Artifact Registry, Secret Manager, the Cloud Run service with its runtime service account, and the Cloud Build trigger, all named `scrapgo-core-api-<env>`. See `infra/terraform/README.md` for bootstrap, proxy access, the Quickbase token and migrations.
- `cloudbuild.yaml` (build context `backend/`): restore, build, test (Testcontainers via the Docker socket), image, push to Artifact Registry, `gcloud run deploy`. It is triggered only by pushes that touch `backend/`.
- `Dockerfile`: a multi-stage .NET 10 build running as a non-root user on port 8080.

## Common Commands

```bash
dotnet build ScrapGo.Core.slnx
dotnet test ScrapGo.Core.slnx          # needs Docker (Testcontainers)

# Migrations: audit schema first, then modules
dotnet ef migrations add <Name> --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure --context IdentityDbContext --output-dir Persistence/Migrations
dotnet ef database update --project src/Shared/ScrapGo.Core.Shared.Infrastructure --context AuditDbContext --connection "<conn>"
dotnet ef database update --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure --context IdentityDbContext --connection "<conn>"
dotnet ef database update --project src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure --context QuickbaseDbContext --connection "<conn>"
```

Required configuration: `ConnectionStrings__Default`, `Gcip__ProjectId`. Required on first Quickbase call: `Quickbase__RealmHostname`, `Quickbase__UserToken`. Optional: `Quickbase__QueryCache__Ttl`, `Quickbase__QueryCache__ServeStaleOnError`, `INTERNAL_HD_ALLOWLIST`, `Identity__RequireMfaForExternalUsers` (default `false`), `Cors__AllowedOrigins__0..n`, `Swagger__Enabled`, `PORT`.
