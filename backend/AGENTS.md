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

- **Organization context comes from the route, never the token.** Any route with an `{organizationId}` segment is covered by `OrganizationMembershipGuardMiddleware` (403 `no_active_membership` without an active membership). Name the segment exactly `organizationId`: a route using `{orgId}` silently gets no guard.
- **Protect endpoints with permissions, not role names:** `[RequirePermission(Permissions.X)]` (organization-scoped, resolved against the route's `{organizationId}`), or `[RequirePermission(Permissions.X, PlatformScope = true)]` (only platform-scoped assignments, those with no organization, satisfy it). The only role-name literal allowed is in `DefaultRoleNames`.
- **Role management** (`RoleService`) is permission-based, never by role name. The caller needs an active membership in the role's organization and, there: `Role.Create` to create, `Role.Update` to edit or attach/detach permissions, `Role.Delete` to delete. (`Role.Assign` is reserved for assigning roles to users.)
- **Escalation guard:** a caller may only attach a permission they already hold in that organization (403 `cannot_grant_unheld_permission`), and may never edit, delete or recompose a role they hold themselves (403 `cannot_modify_own_role`). Keep both on any new role or assignment write path.
- **Built-in roles** (`DefaultRoleNames`, platform-defined, not editable over HTTP). Their permissions are seeded by migration:
  - `OrganizationAdministrator` (id 1): every catalog permission except `Admin.Access`. Granted only per organization, to whoever creates it.
  - `PlatformAdministrator`: `User.*`, `Role.*`, `Admin.Access`. Assigned only at platform scope (`organization_id` NULL).
- **No role is ever assigned at sign-in.** Deny-by-default: a freshly provisioned user holds nothing. The first `PlatformAdministrator` is granted only by the explicit, one-time, audited `bootstrap-platform-admin` host command (see README). It is never an HTTP endpoint.
- **The permission catalog** (`Permissions.All`) is seeded by migration with positional ids, and is read-only over HTTP. Append new permissions; never reorder.
- **Permission cache:** `IPermissionCache` is a pass-through until Redis is migrated. Every mutation that changes a user's effective permissions must invalidate the affected scopes.

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

Required configuration: `ConnectionStrings__Default`, `Gcip__ProjectId`. Required on first Quickbase call: `Quickbase__RealmHostname`, `Quickbase__UserToken`. Optional: `Quickbase__QueryCache__Ttl`, `Quickbase__QueryCache__ServeStaleOnError`, `INTERNAL_HD_ALLOWLIST`, `Cors__AllowedOrigins__0..n`, `Swagger__Enabled`, `PORT`.
