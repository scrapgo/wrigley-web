# ScrapGo Core API (Downstream Backend)

## Architectural Overview

The `ScrapGo.Core.Api` is a Modular Monolith built with **.NET 10** and **PostgreSQL**. Its primary responsibility is acting as a secure, high-performance **Identity Hub and Intelligent Proxy** for **Quickbase**.

While Quickbase remains the System of Record for core business entities (Suppliers, Loads, Pricing, Carriers, and Media), this API abstracts Quickbase behind a clean domain layer, injecting governance, role-based access control, and aggressive caching to bypass Quickbase performance limits.

## Core Design Principles

1. **Proxy & Abstraction Layer:** The API translates clean domain requests into Quickbase API calls. The frontend never talks directly to Quickbase.

2. **PostgreSQL as Infrastructure & Governance:** Postgres strictly manages internal app state: Users, Roles, Permissions, Quickbase Definitions, Access Control lists, Action History, and Query Builder Metadata/Caches.

3. **Smart Caching & Query Builder:** To combat Quickbase latency, the Query Builder service caches query metadata and results in Postgres, serving cached data unless a configurable Time-To-Live (TTL) expires.

4. **Security-First:** Stateless JWT authentication (Google Cloud Identity Platform) with Role-Based Access Control (RBAC) enforced before any request hits Quickbase.

## Postgres Database Scope (Stored Locally)

- **Identity & Access:** Users, Roles, Permissions, Quickbase Table Access.

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

Migrated modules: **Identity**: GCIP auth, user provisioning, the disabled-user gate, global roles and the permission catalog, and the `[RequirePermission]` engine.

**QuickbaseEngine**: implements `IQuickbaseQueryService`, cache-aside over the Quickbase REST API, backed by `quickbase.query_caches`. Its Api project has no controllers.

**Suppliers** (Application, Infrastructure, Api; no Domain or database, since Quickbase owns the data): read endpoints over the Quickbase Suppliers table `bqrcgnatz`, under `/api/suppliers`, for holders of `suppliers.view_all` for now. Supplier scoping (B13 in `docs/ACCESS-MODEL-TASKS.md`) adds `suppliers.view_own`.
- `GET /api/suppliers` returns names sorted by name, with `search`, `skip` and `top` (1–1000, default 100).
- `GET …/suppliers/{recordId}` returns one supplier's details.
- `GET …/suppliers/{recordId}/call-prospect-status` returns call & prospect status (fields 197, 192, 193, 236, 181, 238, 97).
- `GET …/suppliers/{recordId}/yard-capabilities` returns yard capabilities (13 checkboxes).
- `GET …/suppliers/{recordId}/target-pricing-progress-rail` returns Target Pricing — Progress Rail (text, currency and numeric fields 352, 342, 336, 337, 339, 340, 341, 346, 345, 347, 348, 349, 358, 363).
- `GET …/suppliers/{recordId}/in-stock-items` returns the supplier's items from the **In-Stock Items** table `bscpxjrw3` (`{15.EX.'<recordId>'}`), for Materials Pricing: a list with four dropdowns (`InStockItemDropdowns`; field 10 is returned as both `packaging` and `prep`). An empty list, never 404.
- `GET …/suppliers/{recordId}/contacts` returns the supplier's contacts from the **Contacts** table `br8imsxs2` (`{29.EX.'<recordId>'}`), main contact first (sorted in the API). An empty list, never 404.
- `GET …/suppliers/{recordId}/call-notes-history` returns the supplier's notes from the **Call Notes** table `br8tgw3tp` (`{19.EX.'<recordId>'}`), newest first (sorted in the API). An empty list, never 404.
- `GET …/suppliers/{recordId}/linked-its-dispatch-records` returns the ITS dispatch records linked to the supplier from table `bspsb9kpm` (`{366.EX.'<recordId>'}`), newest ship date first; `consumer` is "Consumer - City, State". An empty list, never 404.
- `GET …/suppliers/{recordId}/other-prospect-information` returns Other Prospect Information (Documents & Other): Suppliers fields 77, 152, 175, 157 (text) and 215 (checkbox), plus `latestActivity` from the **Call Notes** table `br8tgw3tp` (`{19.EX.'<recordId>'}`, field 1 descending, top 1; fields 3, 19, 57, 1), or `null` when there is none. This is two queries, because Quickbase can't join: an unknown supplier gives 404 without the second query, and `freshness` is the less fresh of the two.
- All of them share `SupplierService.ReadRecordAsync`: the access check, then 404 / 502.
- `ISupplierSource` (Application) is implemented by `QuickbaseSupplierSource` (Infrastructure), which holds the field ids (`SuppliersTable`) and the JSON mapping.
- Errors: 403 `missing_permission`, 404 `supplier_not_found`, 502 `quickbase_unavailable`.
- **Every supplier dropdown is sent and accepted as its exact text.** Each enum member carries the text in `[JsonStringEnumMemberName("…")]`, which is the single source for the JSON value, the label (`DropdownCatalog<TEnum>.FromJsonNames()`) and the Quickbase text.
- **Payment terms** are the `PaymentTerms` enum (Application), e.g. `"Net 5"`.
  - `PaymentTermsCatalog` holds the labels, which are also the exact Quickbase text in field 320. `QuickbasePaymentTerms` (Infrastructure) converts them.
  - Unknown Quickbase text is returned as `null` and logged.
  - To add a value: append it and its label. Never rename one.
  - `GET /api/suppliers/payment-terms` lists the dropdown options.
- **Dropdown fields** (last call result 193, supplier objection 236) are enums with a `DropdownCatalog<TEnum>` of labels.
  - Each label is the exact Quickbase text. `QuickbaseDropdowns` converts in both directions.
  - Unknown Quickbase text is returned as `null` and logged.
  - Options are served at `GET /api/suppliers/last-call-results` and `GET /api/suppliers/supplier-objections`.
  - New dropdowns follow this pattern.
- **Dead freight** is the `DeadFreight` enum: `"Exempt"` and `"Not Exempt"`.
  - Quickbase field 321 is a checkbox: **checked is `NotExempt`**, unchecked `Exempt` (despite the legacy name `DeadFreightExempt`). `QuickbaseDeadFreight` (Infrastructure) converts it.
  - `GET /api/suppliers/dead-freight` lists the dropdown options.

**Proxy modules** (Suppliers, and later Freight and others) follow the Suppliers shape:
- They reach Quickbase only through `IQuickbaseQueryService` and check the caller through `IUserContext`. Both contracts live in **Shared.Kernel** (`Shared.Kernel.Quickbase`, `Shared.Kernel.Security`), so no module references QuickbaseEngine's or Identity's projects.
- They check permissions with `IUserContext.HasPermissionAsync`, and filter suppliers by `IUserContext.GetSupplierScopeAsync` (`SupplierScope.All` with `suppliers.view_all`, the caller's linked Record IDs with `suppliers.view_own`, `None` otherwise).
- They name Identity's permissions by string, with a spec pinning that string to Identity's `Permissions`.

## Quickbase Query Cache (QuickbaseEngine module)

- **Always go through `IQuickbaseQueryService`** (the contract is in `Shared.Kernel.Quickbase`, with `QuickbaseQuery` and `QuickbaseApiException`), never `IQuickbaseClient` or `HttpClient` directly from a controller. The service hashes the query (`QueryKey`: SHA-256 of the realm plus the canonical query JSON), serves a cache row younger than `Quickbase:QueryCache:Ttl` (default 15 minutes), and otherwise calls Quickbase and upserts the row (`INSERT ... ON CONFLICT`, where the newest response wins).
- **Failures are never cached.** With `Quickbase:QueryCache:ServeStaleOnError` (on by default), a failed refresh serves the expired row instead, flagged `StaleCache`.
- **UserContext gate.** `QuickbaseQueryService` checks `IUserContext` (defined in Shared.Kernel, implemented by Identity) before reading the cache or calling Quickbase. An unauthenticated or non-active caller gets `UnauthorizedAccessException`, which the host returns as 403 `access_denied`. That check is the floor. One Quickbase credential serves all users and cache rows are shared, so table-level access (`IUserContext.HasPermissionAsync`) must still be checked by the caller until the Quickbase table-access model exists.
- **Configuration:** `Quickbase__RealmHostname` and `Quickbase__UserToken` (a secret, from Secret Manager) are validated on first use, not at startup. Never read `QuickbaseOptions` from anything that runs at startup, or every host will need the credentials to boot.
- **Resilience (Polly v8, `AddStandardResilienceHandler`):** transient failures (408, 429 honoring `Retry-After`, 5xx, network errors, timeouts) are retried with exponential backoff and jitter; other 4xx responses are not. There is also a per-attempt timeout, a total timeout and a circuit breaker. Tune them with `Quickbase__Timeout` (per attempt), `Quickbase__MaxRetryAttempts` and `Quickbase__RetryBaseDelay` (`QuickbaseResilienceOptions`). Retries are safe only because every call on this client is a read; give any future write call a pipeline with `Retry.DisableForUnsafeHttpMethods()`.
- **Changing the canonical query shape:** bump `QueryKey.Version`, which orphans the old keys.

## Authorization Model (Identity module)

This section describes an interim model, being replaced by [`../docs/ACCESS-MODEL.md`](../docs/ACCESS-MODEL.md) task by task (`docs/ACCESS-MODEL-TASKS.md`). Applications, modules and the catalog are gone (B2), and so are organizations, memberships and organization invitations (B3).

**The schema is the new one (B5, B6).** The entities and configurations match section 5 of `ACCESS-MODEL.md`: `User` (with `UserType`), `Role`, `UserRole`, `Feature`, `FeatureTarget`, `Permission`, `RolePermission`, `UserSupplier` and `Invitation`. The forward migration `AccessModelV1` drops the old tables, keeps users and seeds section 6 from `AccessModelSeed` (Infrastructure), using the keys in `Permissions`, `DefaultRoleNames` and `FeatureKeys` (Domain). The seed has fixed ids; the migration moves each identity sequence past them. **The access context (B7)** is the new engine's core: `AccessContext` (Application/Access) applies the access rule of section 4 (active user AND a role grants the permission AND its feature is on for them). `EfAccessContextProvider` loads it in one EF query; `MemoryAccessCache` caches it by UID for 30 seconds, with `EvictUser(userId)` for a change to one user and `EvictAll()` for a role-permission or feature change; `ICurrentAccess` (`HttpCurrentAccess`, request-scoped) loads the caller's at most once per request. The `[RequirePermission]` engine below is built on it (B8).

### Permissions and resolution

- **`[RequirePermission(params string[] anyOf)]`** sets a `perm:a|b` policy; `PermissionPolicyProvider` builds it on demand (any of the keys), and `PermissionHandler` checks it against `ICurrentAccess`. The only role-key literals allowed are in `DefaultRoleNames`; code checks permission keys only.
- **Denials:** `PermissionDeniedResultHandler` turns every authorization 403 into ProblemDetails with `reason: missing_permission` and writes an `access.denied` audit row (method, path, required keys).
- **The disabled-user gate** (`DisabledUserGateMiddleware` → `EvaluateUserStatusGateHandler`) reads the same `ICurrentAccess`: a known user who isn't Active gets 403 `account_disabled` (audited as `access.denied`). Status changes evict the user from the cache, so they apply on the very next request.
- **Sessions (B10):** `POST /api/session` (`SessionController` → `StartSessionHandler`) ends every sign-in and is the only place users are created. An unknown UID becomes an Internal user with `scrapgo_user` only when the token's provider is `google.com`, `email_verified` is true and `hd` is on the allow-list, exactly once via `InsertIfAbsentAsync`; anything else is 403 `no_access` with no row. A known user's sign-in is recorded (`session.started`). `GET /api/me` (`MeController` → `GetMeHandler`) returns `{ user, roles, permissions, features }` and never creates a user (403 `no_access` for an unknown UID).
- **Invitations (B11):** `/api/admin/invitations` (`InvitationsController` -> `InvitationService`): list and get need `users.view` or `users.invite`; create, resend and revoke need `users.invite`. The role must be External, with one or more supplier Record IDs. Tokens are 32 random bytes, base64url (`InvitationTokens`); only the SHA-256 is stored; 7-day expiry; resend issues a new token. Create and resend return the accept link (`{Invitations:FrontendBaseUrl}/invite/accept?token=...`) for the admin to copy; `IInvitationSender` (V1 `LoggingInvitationSender`, which never logs the link) is where real email will plug in. Acceptance is `POST /api/session` with `{ "invitationToken": "..." }`: the invitation row is locked, then it must be Pending, unexpired, `email_verified`, and the token's email must match (case-insensitive); the External user, role, supplier links and acceptance commit in one transaction. Any refusal is 403 `no_access`, with the specific cause only in the `access.denied` audit row.
- **Other modules** check through `IUserContext.HasPermissionAsync`, also backed by `ICurrentAccess`.
- **There is no per-request Google Workspace check any more.** Internal is a user type, decided once at creation (ACCESS-MODEL.md section 3).
- **`INTERNAL_HD_ALLOWLIST`** decides who `POST /api/session` may create as Internal. It is a JSON array in appsettings (`["scrapgo.com"]`, as in `appsettings.Development.json`) or a comma-separated string as an env var (`InternalHdAllowlistConfiguration.Read`). GCIP tokens only carry `hd` because of the `beforeSignIn` blocking function in `infra/gcip-blocking-function/`.
- **Permissions (code):** `Permissions` (Domain) holds every key; the seed maps them to features and roles.
- **A FallbackPolicy** requires an authenticated caller on any endpoint without authorization metadata. Only `/healthz` and `/healthz/ready` are `AllowAnonymous`.

### Roles

- **Three system roles** (`site_admin`, `scrapgo_user`, `user`), seeded; there is no role create or delete (B15 builds the roles API).
- **Assigning and revoking** (`UserRoleAssignmentService`, `/api/users/{id}/roles`) needs `users.manage` (403 `platform_admin_required` otherwise). The escalation guard refuses a role with a permission the caller doesn't hold (403 `cannot_grant_unheld_permission`). External users can't hold internal roles (`external_user_not_allowed`).
- **Last-administrator protection** (409 `last_platform_administrator`, under a row lock): the last active Site Admin can't be revoked or disabled.
- **Deny-by-default.** The first Site Admin comes only from the one-time, audited `bootstrap-platform-admin` host command.
- **External users:** MFA for them is reserved behind `Identity:RequireMfaForExternalUsers`, off until MFA is enabled in GCIP. When it's on, a token without `firebase.sign_in_second_factor` gets 403 `mfa_required`.
- **Cache eviction:** every change to a user's roles or status calls `IAccessCache.EvictUser`; role-permission and feature changes must call `EvictAll`.

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
5. Authentication, the Identity gate (disabled user), authorization
6. `/healthz` (liveness, no dependencies; Cloud Run's probes) and `/healthz/ready` (Postgres checks for every module DbContext), then controllers

Not ported yet, because their modules or Redis aren't migrated: rate limiting, the audit export job, and the invitations, join-request, member-admin, platform-role, audit-log and enterprise-SSO endpoints.

The admin-portal API surface (user listing, role listing/read-back, role assignment, user status) is also not migrated; see [`ADMIN-API-GAPS.md`](ADMIN-API-GAPS.md) for the full list and implementation order.

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
