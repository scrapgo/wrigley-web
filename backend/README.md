# ScrapGo Core API

The backend for the ScrapGo Downstream Portal: a .NET 10 modular monolith that
is the portal's **identity hub** (Google Cloud Identity Platform sign-in,
organizations, roles and permissions) and its **governed, cached proxy** to
Quickbase. The frontend talks only to this API, never to Quickbase.

## Current Status

For detailed implementation status, see [Backend Implementation Status](BACKEND-IMPLEMENTATION-STATUS.md).

- Architecture, rules and conventions: [`AGENTS.md`](AGENTS.md)
- GCP infrastructure and deployment: [`infra/terraform/README.md`](infra/terraform/README.md)
- Organizations, applications and modules, on the site and in code: [`../CATALOG-AND-ADMIN-GUIDE.md`](../CATALOG-AND-ADMIN-GUIDE.md)
- Workspace sign-in hook for platform admins: [`infra/gcip-blocking-function/README.md`](infra/gcip-blocking-function/README.md)

Modules (`src/Modules/`):

| Module              | What it does                                                                                                                                           |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Identity**        | Sign-in, users, organizations, roles and permissions, applications and modules, invitations                                                            |
| **QuickbaseEngine** | The only path to Quickbase: `IQuickbaseQueryService`, cached in Postgres (`quickbase.query_caches`) and resilient (retries, timeouts, circuit breaker) |
| **Suppliers**       | Read endpoints over the Quickbase Suppliers table (`bqrcgnatz`): list, details, call & prospect status, yard capabilities, Target Pricing — Progress Rail, and dropdown options. See [Suppliers](#suppliers) |

Proxy modules like Suppliers reach Quickbase and the caller only through
contracts in `Shared.Kernel`: `IQuickbaseQueryService` (`Shared.Kernel.Quickbase`)
and `IUserContext` (`Shared.Kernel.Security`). They never reference another
module's projects.

---

## Prerequisites

| Tool                 | Why                                                           | Check                       |
| -------------------- | ------------------------------------------------------------- | --------------------------- |
| .NET SDK 10.0.101+   | Build and run (pinned in `global.json`)                       | `dotnet --version`          |
| Google Cloud SDK     | Sign in to GCP; install the Cloud SQL Auth Proxy              | `gcloud --version`          |
| Cloud SQL Auth Proxy | Reach the Cloud SQL Postgres database from your machine       | `cloud-sql-proxy --version` |
| Docker Desktop       | Only for the test suite (Testcontainers) and container builds | `docker version`            |

There is **no local database**: the API always runs against Postgres in Cloud
SQL, reached through the Cloud SQL Auth Proxy.

## Running the API locally

### 1. One-time setup

```bash
# Sign in: the first login is for gcloud itself, the second is for the proxy.
gcloud auth login
gcloud auth application-default login
gcloud config set project wrigley-cloud-prod

# Install the Cloud SQL Auth Proxy (or download it from Google's docs).
gcloud components install cloud-sql-proxy

# Tell the API where the database is. The connection string is stored with
# `dotnet user-secrets` in your user profile, outside the repo, and is never committed.
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=127.0.0.1;Port=5434;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Disable" \
  --project src/ScrapGo.Core.Api
```

Ask a teammate for the database name, user and password, or read them from
the environment's Secret Manager secret (see
[`infra/terraform/README.md`](infra/terraform/README.md#connecting-from-a-developer-machine-cloud-sql-auth-proxy)).
`SSL Mode=Disable` is correct here: the proxy encrypts the connection to Cloud
SQL, and only the hop to `127.0.0.1` is plain.

Check what is stored with `dotnet user-secrets list --project src/ScrapGo.Core.Api`.

### 2. Start the Cloud SQL Auth Proxy

Keep this running in its own terminal:

```bash
cloud-sql-proxy <project>:<region>:<instance> --port 5434
# e.g. the current dev database:
cloud-sql-proxy wrigley-cloud-prod:us-central1:identity-platform-dev --port 5434
```

### 3. Run the API

From `backend/`:

```bash
dotnet run --project src/ScrapGo.Core.Api
```

It starts on **http://localhost:5141**. The `https` launch profile also serves
https://localhost:7021: `dotnet run --project src/ScrapGo.Core.Api --launch-profile https`.
In VS Code or Visual Studio, run the `ScrapGo.Core.Api` project with the `http`
profile. It opens Swagger automatically.

### 4. Check it works

| URL                                 | Expect                                                                                    |
| ----------------------------------- | ----------------------------------------------------------------------------------------- |
| http://localhost:5141/swagger       | Swagger UI listing every endpoint                                                         |
| http://localhost:5141/healthz       | `200` with `"status":"Healthy"` (the process is up)                                       |
| http://localhost:5141/healthz/ready | `200` once the database is reachable; `503` means the proxy or connection string is wrong |
| http://localhost:5141/api/users/me  | `401` without a token (authentication is enforced)                                        |

### 5. Running the Frontend

The frontend is a React application located in the `frontend/` directory at the root of this repository. To run it:

```bash
cd ../frontend
npm install
npm run dev
```

The frontend starts on http://localhost:5173 and its dev server proxies `/api` to this API at http://localhost:5141, so CORS isn't involved.

**Start the API first** and wait for `Now listening on: http://localhost:5141`. If the portal loads before the API is up, Vite logs `http proxy error … ECONNREFUSED`; refresh once the API is listening.

### 6. Call authenticated endpoints

Every `/api/**` endpoint needs a **Google Cloud Identity Platform ID token**
for project `wrigley-cloud-prod`, sent as `Authorization: Bearer <token>`.
Get one by signing in through the portal (or any GCIP client), then either:

- **Swagger:** click **Authorize** and paste the token without the `Bearer ` prefix.
- **VS Code / Visual Studio:** open [`src/ScrapGo.Core.Api/ScrapGo.Core.Api.http`](src/ScrapGo.Core.Api/ScrapGo.Core.Api.http), paste the token into `@token`, and send the requests.

Start with `GET /api/users/me`: the first call provisions your user record.

### 7. Bootstrap the first platform administrator (once per database)

Platform-wide admin routes (`Admin.Access` at platform scope) deny everyone
until someone holds the built-in `PlatformAdministrator` role. Nobody gets a
role by signing in, so the first one is granted by an explicit host command:

1. Apply the Identity migrations (see [Database migrations](#database-migrations)).
2. Sign in as the person to promote, **with "Sign in with Google" using their
   Workspace (`@scrapgo.com`) account**, and call `GET /api/users/me` once.
   That provisions their user record as Internal. Platform roles are for
   Internal users only, and platform access needs a Workspace sign-in on every
   request (see [Platform administrators](#platform-administrators-google-workspace-sign-in)).
   Their GCIP UID is the token's `sub` claim (also `identityPlatformUid` in the
   `/api/users/me` response).
3. With the Cloud SQL proxy running and user-secrets pointing at the target
   database, run:

   ```bash
   dotnet run --project src/ScrapGo.Core.Api -- bootstrap-platform-admin --uid <GCIP uid>
   ```

   The command does not start the web server. It prints one of:

   | Output                       | Exit code | Meaning                                                                                                                   |
   | ---------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------- |
   | `Granted`                    | 0         | The user now holds `PlatformAdministrator`. An audit row was written.                                                     |
   | `AlreadyGranted`             | 0         | They already held it. Nothing changed; it is safe to re-run.                                                              |
   | `UserNotProvisioned`         | 1         | No user for that UID. Do step 2 first.                                                                                    |
   | `AnotherAdministratorExists` | 1         | Someone else already holds it. Bootstrap is one-time; it never adds a second admin.                                       |
   | `ExternalUserNotAllowed`     | 1         | The user is External (their first sign-in wasn't a Workspace Google sign-in). Platform roles are for Internal users only. |

Permissions are resolved per request, so the new admin's next request already
carries `Admin.Access`. No new token is needed.

Further platform admins are granted by an existing one, not by this command:
`POST /api/users/{id}/roles` with the PlatformAdministrator role id and no
`organizationId`.

### 8. Platform administrators: Google Workspace sign-in

Platform-scoped permissions resolve only when the request's token carries an
`hd` claim on `INTERNAL_HD_ALLOWLIST` (`scrapgo.com`), on **every request**:

- **Email/password sign-ins never carry `hd`.** A platform admin signed in that
  way gets 403 `workspace_sign_in_required` on platform routes. `/api/users/me`
  shows no platform roles but returns `workspaceSignInRequired: true`, and the
  portal shows a "Sign in with Google" notice.
- **GCIP doesn't put `hd` in its tokens by itself.** The `beforeSignIn` blocking
  function in [`infra/gcip-blocking-function/`](infra/gcip-blocking-function/README.md)
  copies it from Google's token for Google sign-ins.
  - It's deployed as `beforeSignIn` (us-central1) and registered under Identity Platform → Settings → Triggers.
  - Without it, nobody has platform access.
- Organization and application access work with any sign-in method.

### 9. Connect to Quickbase

Only needed for Quickbase-backed endpoints such as `/api/suppliers`; everything
else runs without it. From `backend/`:

```bash
# The realm isn't a secret, but user-secrets keeps it out of committed files.
dotnet user-secrets set "Quickbase:RealmHostname" "scrapgo.quickbase.com" --project src/ScrapGo.Core.Api

# A Quickbase *user token*, assigned to the app that holds the tables you query.
dotnet user-secrets set "Quickbase:UserToken" "<user token>" --project src/ScrapGo.Core.Api
```

Get the token in Quickbase: your name → **My preferences** → **Manage my user
tokens** → **+ New user token**, assigned to the app that contains the
Suppliers table. Paste it without any `QB-USER-TOKEN` prefix.

**Check the token before starting the API.** This sends the same query
straight to Quickbase:

```bash
T=$(dotnet user-secrets list --project src/ScrapGo.Core.Api | sed -n 's/^Quickbase:UserToken = //p')
curl -s -X POST https://api.quickbase.com/v1/records/query \
  -H "QB-Realm-Hostname: scrapgo.quickbase.com" -H "Authorization: QB-USER-TOKEN $T" \
  -H "Content-Type: application/json" -d '{"from":"bqrcgnatz","select":[3,8],"options":{"top":1}}'
```

| Result                             | Meaning                                                                                |
| ---------------------------------- | -------------------------------------------------------------------------------------- |
| JSON with `data`                   | The token works. Restart the API (settings are read at startup)                        |
| `401` `"User token is invalid"`    | Quickbase doesn't know this token (deleted, regenerated or mistyped). Create a new one |
| `401`/`403` about the app or table | The token isn't assigned to the app that holds the table                               |

Then, signed in to the portal as a platform admin with Google, take
`localStorage.authToken` and call:

```bash
curl -s "http://localhost:5141/api/suppliers?search=Auto&top=5" -H "Authorization: Bearer <token>"
curl -s http://localhost:5141/api/suppliers/17511 -H "Authorization: Bearer <token>"
curl -s http://localhost:5141/api/suppliers/17511/call-prospect-status -H "Authorization: Bearer <token>"
curl -s http://localhost:5141/api/suppliers/17511/yard-capabilities -H "Authorization: Bearer <token>"
curl -s http://localhost:5141/api/suppliers/17511/target-pricing-progress-rail -H "Authorization: Bearer <token>"
```

## Configuration

Non-secret development values live in `src/ScrapGo.Core.Api/appsettings.Development.json`.
Secrets live in `dotnet user-secrets` locally and Secret Manager in GCP;
**they never go in a committed file**. The full list, as environment variables,
is in [`.env.example`](.env.example).

| Setting                               | Required                   | Local default           | Purpose                                                                                                                                                                                                                                                                                                                                               |
| ------------------------------------- | -------------------------- | ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default`           | Yes                        | user-secrets            | Postgres (Npgsql) connection string                                                                                                                                                                                                                                                                                                                   |
| `Gcip:ProjectId`                      | Yes                        | `wrigley-cloud-prod`    | GCIP project whose ID tokens are accepted                                                                                                                                                                                                                                                                                                             |
| `Quickbase:RealmHostname`             | On first Quickbase call    | user-secrets            | `scrapgo.quickbase.com`. Empty in `appsettings.json`, so set it (see step 9)                                                                                                                                                                                                                                                                          |
| `Quickbase:UserToken`                 | On first Quickbase call    | user-secrets            | Quickbase user token (secret), assigned to the app that holds the tables                                                                                                                                                                                                                                                                              |
| `Quickbase:QueryCache:Ttl`            | No                         | `00:15:00`              | How long cached Quickbase results are served                                                                                                                                                                                                                                                                                                          |
| `Cors:AllowedOrigins`                 | No                         | `http://localhost:3000` | Browser origins allowed to call the API                                                                                                                                                                                                                                                                                                               |
| `INTERNAL_HD_ALLOWLIST`               | **Yes for platform admin** | —                       | Google Workspace domains treated as internal users. Platform access also needs a sign-in whose token carries one of these as `hd` on every request: when blank, nobody has platform access. `hd` comes from the GCIP blocking function in [`infra/gcip-blocking-function/`](infra/gcip-blocking-function/README.md). Terraform default: `scrapgo.com` |
| `Swagger:Enabled`                     | No                         | `true`                  | Serves `/swagger` (off in Cloud Run unless set)                                                                                                                                                                                                                                                                                                       |
| `Identity:RequireMfaForExternalUsers` | No                         | `false`                 | When `true`, external users need a second factor (`mfa_required` otherwise). Keep off until MFA is enabled in GCIP                                                                                                                                                                                                                                    |

Quickbase settings are validated on the first Quickbase call, not at startup,
so the API runs without them; see [step 9](#9-connect-to-quickbase).
Set the allowlist locally the same way:
`dotnet user-secrets set INTERNAL_HD_ALLOWLIST scrapgo.com --project src/ScrapGo.Core.Api`.

## Database migrations

Each module owns its own schema and migrations. With the proxy running, apply
them in this order (the audit schema first). `$C` lasts only for the current
terminal, so set it from your user-secrets each time:

```bash
C=$(dotnet user-secrets list --project src/ScrapGo.Core.Api | sed -n 's/^ConnectionStrings:Default = //p')
dotnet ef database update --project src/Shared/ScrapGo.Core.Shared.Infrastructure --context AuditDbContext --connection "$C"
dotnet ef database update --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure --context IdentityDbContext --connection "$C"
dotnet ef database update --project src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure --context QuickbaseDbContext --connection "$C"
```

`dotnet ef` comes from `dotnet tool install --global dotnet-ef`. Only run
migrations against a database meant for this API: they create the `audit`,
`identity` and `quickbase` schemas.

If the local API is running, its build output is locked. Add
`--configuration Release` to `dotnet ef` commands (they then build into
`bin/Release`), or stop the API first.

Recent Identity migrations, in order:

| Migration                              | What it does                                                                                                                                                                                                                              |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RetireGenericPermissions`             | Removes the retired `Invoice.*` / `Report.*` permissions from roles (audited); the rows stay because ids are positional                                                                                                                   |
| `AddPlatformAdministrationPermissions` | `Organization.Create`, `Organization.Deactivate`, `Application.Assign`, `Module.Manage`, `Catalog.Manage` for PlatformAdministrator, plus `Application.ManageAccess`                                                                      |
| `AddApplicationsAndModules`            | Catalog and entitlement tables; application roles and grant expiry                                                                                                                                                                        |
| `AddInvitations`                       | Email invitations with pre-granted roles                                                                                                                                                                                                  |
| `AddDownstreamApplication`             | The first catalog application, **Downstream**: modules Pricing, Opportunities, Loads & Freight and Suppliers; permissions `Downstream.<Module>.Read/Write` (ids 25–32); role templates "Downstream Administrator" and "Downstream Viewer" |

## Tests

```bash
dotnet test ScrapGo.Core.slnx
```

The integration tests start their own throwaway Postgres containers
(Testcontainers), so **Docker must be running**, and they never touch Cloud SQL.
The full run takes about 20 minutes (Identity: ~400 tests; QuickbaseEngine: 30; Suppliers: ~100).
The Suppliers tests run the whole host with the real permission engine and a
fake `IQuickbaseQueryService` fed real Quickbase responses, so they never call
Quickbase.
While it runs, the test host locks the test projects' DLLs, so don't
`dotnet build` at the same time.

To run tests while the local API is running, build them into a separate folder
so the API's locked DLLs aren't touched:

```bash
dotnet test tests/Modules/ScrapGo.Core.Modules.Identity.IntegrationTests -o "$TEMP/scrapgo-testout" --filter "FullyQualifiedName~DownstreamWalkthrough"
```

`Applications/DownstreamWalkthrough.cs` runs the guide's whole test scenario
against the real Downstream catalog: create organizations, assign, enable,
appoint, grant, disable, deactivate and remove.

## Running in Docker

```bash
docker build -t scrapgo-core-api .
cp .env.example .env.local          # fill in; .env.local is git-ignored
docker run --rm -p 8080:8080 --env-file .env.local scrapgo-core-api
```

Inside the container, the proxy on your machine is `host.docker.internal:5434`
(already set in `.env.example`). The API listens on http://localhost:8080.

## Troubleshooting

| Symptom                                                                                | Cause / fix                                                                                                                                                                         |
| -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default is not configured` at startup                               | user-secrets not set: see step 1. Run from `backend/` with `--project src/ScrapGo.Core.Api`.                                                                                        |
| `Gcip:ProjectId is not configured`                                                     | Running with an environment other than Development and no `Gcip__ProjectId` set.                                                                                                    |
| `/healthz/ready` returns `503`                                                         | The proxy isn't running, is on another port, or the credentials are wrong.                                                                                                          |
| Proxy: `could not find default credentials`                                            | Run `gcloud auth application-default login`.                                                                                                                                        |
| Proxy can't connect to the instance                                                    | The instance has no proxy-reachable IP (private-IP only). See the infra README.                                                                                                     |
| Every `/api/**` call returns `401`                                                     | Missing or expired token (tokens last at most 1 hour), or it was issued for another GCIP project.                                                                                   |
| `403` with `reason: user_disabled`                                                     | Your user record is disabled.                                                                                                                                                       |
| `dotnet build` fails with "file is locked by testhost"                                 | A test run is still going. Wait for it or stop it.                                                                                                                                  |
| `401` and the request shows `Authorization: Bearer "@token` (or similar)               | A placeholder was pasted into Swagger's **Authorize** box. Paste the raw ID token (`eyJ…`) with no quotes and no `Bearer ` prefix.                                                  |
| `403` with `reason: workspace_sign_in_required`                                        | A platform route was called with a token that has no allowlisted `hd`. Sign in with Google using a `@scrapgo.com` account; check `INTERNAL_HD_ALLOWLIST` and the blocking function. |
| `dotnet build` / `dotnet ef` fails: file locked by `ScrapGo.Core.Api`                  | The local API is running. Stop it, or use `--configuration Release` (ef) or `-o <folder>` (test).                                                                                   |
| `ConnectionString property has not been initialized` from `dotnet ef`                  | `$C` is empty in this terminal. Set it from user-secrets (see [Database migrations](#database-migrations)).                                                                         |
| `28P01: password authentication failed for user "<user>"`                              | The connection string still has placeholders. Use the real values from user-secrets or Secret Manager.                                                                              |
| `502` with `reason: quickbase_unavailable`                                             | Quickbase failed and nothing was cached. The API log shows the cause after `Quickbase query on table '…' returned`.                                                                 |
| Log: `returned 401: {"message":"Access denied","description":"User token is invalid"}` | The Quickbase user token is dead. Create a new one and check it with the curl in [step 9](#9-connect-to-quickbase). Restarting doesn't help.                                        |
| Quickbase calls fail on `RealmHostname` / `UserToken` validation                       | The Quickbase settings aren't set: see [step 9](#9-connect-to-quickbase).                                                                                                           |

## Access model

Access is scoped by organization, then application, then module:

- **Catalog (code):** applications and modules are defined in code and seeded by migration.
  - The first one is **Downstream** (`Domain/Applications/DownstreamApplication.cs`).
  - Adding one is described in [`../CATALOG-AND-ADMIN-GUIDE.md`](../CATALOG-AND-ADMIN-GUIDE.md), Part 2.
- **Platform administrators** (Google Workspace sign-in):
  - create organizations, naming the first administrator by user id or email (an unknown email gets an invitation)
  - rename them (the slug follows the name)
  - set an administrator (which revokes pending invitations)
  - deactivate and reactivate them
  - **delete** a deactivated one permanently; its audit history is kept
  - assign applications, enable licensed modules, and appoint each application's first administrator
- **Application administrators** grant and revoke application roles in their organization, optionally with expiry. Organization administrators can't.
- **Effective permissions:** a permission resolves only while its module is enabled for that organization's application.
- **Nothing is automatic:** nobody gets anything at sign-in, by joining an organization, or when an application is assigned.

The design and its decisions are in [`../ORG-APP-MODULE-MODEL.md`](../ORG-APP-MODULE-MODEL.md), the rules in [`AGENTS.md`](AGENTS.md) ("Authorization Model"), and the admin API history in [`ADMIN-API-GAPS-v2.md`](ADMIN-API-GAPS-v2.md).

Platform admin endpoints (all `[PlatformAdministration]`, no membership needed):

| Route                                                               | Purpose                                                                                 |
| ------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `GET/POST /api/admin/organizations`                                 | List (search, status, paging) / create                                                  |
| `PUT /api/admin/organizations/{id}`                                 | Rename (regenerates the slug; 409 `duplicate_slug`)                                     |
| `PUT /api/admin/organizations/{id}/administrators/{userId}`         | Make member + OrganizationAdministrator; revokes pending invitations                    |
| `POST /api/admin/organizations/{id}/deactivate` · `/reactivate`     | Block / restore all access                                                              |
| `DELETE /api/admin/organizations/{id}`                              | Permanently delete a **deactivated** organization (409 `organization_active` otherwise) |
| `GET /api/admin/organizations/{id}/applications`                    | Its applications and enabled modules                                                    |
| `PUT/DELETE /api/admin/organizations/{id}/applications/{appId}`     | Assign / remove (removal revokes every grant for it)                                    |
| `PUT/DELETE …/applications/{appId}/modules/{moduleId}`              | Enable / disable a module                                                               |
| `GET …/applications/{appId}/roles`                                  | The application's roles (templates first)                                               |
| `PUT/DELETE …/applications/{appId}/members/{userId}/roles/{roleId}` | Grant / revoke an application role, e.g. the first application administrator            |

Invitations are accepted with `POST /api/invitations/accept` `{ token }`. It needs a verified email matching the invitation's; a Google sign-in counts as verified.

## Suppliers

Read-only endpoints over the Quickbase Suppliers table (`bqrcgnatz`), in the
Suppliers module. They need Quickbase configured ([step 9](#9-connect-to-quickbase)).

### Who can call them

Every endpoint checks the caller before any Quickbase data is read. A caller without access gets 403, and no query runs.

| Route form | Who |
| --- | --- |
| `/api/organizations/{organizationId}/applications/{applicationId}/suppliers…` | Holders of `Downstream.Suppliers.Read` in that organization's application (Downstream is application `1`), with its Suppliers module enabled |
| `/api/suppliers…` | **Platform administrators only** (`Admin.Access` at platform scope, Google Workspace sign-in). The Suppliers table is ScrapGo-wide |
| `/api/suppliers/<dropdown>` options | Any signed-in user (fixed lists, not Quickbase data) |

### Endpoints

Each record endpoint exists in both route forms. For example,
`GET /api/organizations/{id}/applications/1/suppliers/{recordId}/yard-capabilities` and
`GET /api/suppliers/{recordId}/yard-capabilities`.

| Endpoint (after `…/suppliers`) | Returns |
| --- | --- |
| `?search=&skip=&top=` | Supplier names sorted by name, paged (`top` 1–1000, default 100). `search` is a name substring; `totalRecords` is the full count |
| `/{recordId}` | Details (below) |
| `/{recordId}/call-prospect-status` | Call & prospect status (below) |
| `/{recordId}/yard-capabilities` | Yard capabilities (below) |
| `/{recordId}/target-pricing-progress-rail` | Target Pricing — Progress Rail (below) |
| `/payment-terms` · `/dead-freight` · `/last-call-results` · `/supplier-objections` | Dropdown options: `[{ "value": "Net5", "label": "Net 5" }, …]` |

Every record response carries `freshness`:
- `source` is `Cache`, `Quickbase` or `StaleCache`.
- `fetchedAt` says when the data came from Quickbase.

Results are cached for `Quickbase:QueryCache:Ttl` (15 minutes by default).

Errors:
- 403 `missing_permission`
- 404 `supplier_not_found`
- 400 `invalid_request` (out-of-range paging)
- 502 `quickbase_unavailable`: Quickbase failed and nothing was cached. The API log has Quickbase's own message.

### Fields

All four views look up one record with `{3.EX.'<recordId>'}`. Fields are listed in query order, and `null` means empty in Quickbase; empty or blank text is `null` too, never `""`.

**Details** (`supplier`):

| Field | Id | API name | Type |
| --- | --- | --- | --- |
| Record ID# | 3 | `recordId` | number |
| Account | 8 | `account` | text |
| Street Address · City · State · Country · Zip Code | 9 · 10 · 11 · 64 · 12 | `streetAddress` · `city` · `state` · `country` · `zipCode` | text |
| Main Contact Phone to Text | 355 | `mainContactPhone` | text |
| Main Contact Full Name | 123 | `mainContactNames` | list of text |
| Payment Terms | 320 | `paymentTerms` | enum `PaymentTerms` |
| Main email to text | 301 | `mainEmail` | text |
| Lead Assigned To | 74 | `leadAssignedTo` | `{ id, email, name }` |
| # of Relevant Consumer Distances | 28 | `relevantConsumerDistances` | number |
| In Stock Item records | 133 | `inStockItemRecords` | text |
| Total # of Activities | 116 | `totalActivities` | number |
| Target Consumer Price | 346 | `targetConsumerPrice` | number |
| # of Delivered in Last 90 Days · before 90 Days | 129 · 214 | `deliveredLast90Days` · `deliveredBefore90Days` | number |
| Dead Freight | 321 | `deadFreight` | enum `DeadFreight` |

**Call & prospect status** (`callProspectStatus`):

| Field | Id | API name | Type |
| --- | --- | --- | --- |
| Contact with Decision Maker Has Been Made | 197 | `contactWithDecisionMakerMade` | text |
| Prospect Status | 192 | `prospectStatus` | text |
| Last Call Result | 193 | `lastCallResult` | enum `LastCallResult` |
| Supplier Objections | 236 | `supplierObjection` | enum `SupplierObjection` (single choice) |
| Call Back Date | 181 | `callBackDate` | date/time (UTC) |
| Objection Explained | 238 | `objectionExplained` | text |
| Call Notes | 97 | `callNotes` | text |

**Yard capabilities** (`yardCapabilities`): every field is a Quickbase checkbox, returned as `true`/`false`.

| Field | Id | API name |
| --- | --- | --- |
| Crusher on Site? | 65 | `crusherOnSite` |
| Logger on Site? | 186 | `loggerOnSite` |
| Load Flatbeds? | 78 | `loadFlatbeds` |
| Load Dumps? | 182 | `loadDumps` |
| Mobile Crusher | 225 | `mobileCrusher` |
| Can Export? | 204 | `canExport` |
| Has Gaylord Boxes? | 205 | `hasGaylordBoxes` |
| Baler on Site? | 185 | `balerOnSite` |
| Has Scale? | 359 | `hasScale` |
| Load Van Trailers? | 183 | `loadVanTrailers` |
| Has Load Wrap? | 187 | `hasLoadWrap` |
| Use Own Trucks? | 184 | `usesOwnTrucks` |
| Rail Access | 230 | `railAccess` |

**Target Pricing — Progress Rail** (`targetPricingProgressRail`): currency and numeric fields are decimals.

| Field | Id | API name | Type |
| --- | --- | --- | --- |
| Target Material | 352 | `targetMaterial` | text |
| Target Break Even | 342 | `targetBreakEven` | currency |
| Target Offer | 336 | `targetOffer` | currency |
| Target UOM | 337 | `targetUom` | text |
| Trucks / Week | 339 | `trucksPerWeek` | numeric |
| Target FR/UOM | 340 | `targetFreightPerUom` | numeric |
| Target Freight Cost | 341 | `targetFreightCost` | currency |
| Target Consumer Price | 346 | `targetConsumerPrice` | currency |
| Price in Net Tons | 345 | `priceInNetTons` | currency |
| Price in LBS | 347 | `priceInLbs` | currency |
| Price in CWT | 348 | `priceInCwt` | currency |
| Price in Gross Tons | 349 | `priceInGrossTons` | currency |
| Target PO Number | 358 | `targetPoNumber` | text |
| Price Change from Prior | 363 | `priceChangeFromPrior` | currency |

### Dropdowns (enums)

Dropdown fields are enums, sent and accepted by **name** (e.g. `"Net5"`).

| Enum | Quickbase field | Values (label) |
| --- | --- | --- |
| `PaymentTerms` | 320 (text) | `Net5` (Net 5), `Net10` (Net 10), `Net30` (Net 30), `TuesdayThursday` (Tuesday/Thursday), `MlNorwood` (ML Norwood) |
| `DeadFreight` | 321 (checkbox) | `Exempt` (Exempt) = **unchecked**, `NotExempt` (Not Exempt) = **checked** |
| `LastCallResult` | 193 (dropdown) | 14 values, e.g. `NoAnswerVoiceMail` (No Answer - Voice Mail), `PoPending` (PO Pending) |
| `SupplierObjection` | 236 (dropdown) | 22 values, e.g. `PaymentTerms` (Payment Terms), `PastScrapGoIssues` (Past ScrapGo Issues), `SellsToOurConsumer` (Sells to Our Consumer) |

- **Labels:** each label is what the dropdown shows and the **exact text Quickbase stores**. Matching ignores case, extra spaces and curly apostrophes.
- **Unknown values:** Quickbase text that isn't a known value comes back as `null`, with a warning in the log (`… isn't a known value`).
- **Where they're defined:** Application `Suppliers/*.cs` (`PaymentTermsCatalog`, `DropdownCatalog<TEnum>`).
- **Conversion:** Infrastructure `Quickbase/Quickbase*.cs` converts to and from Quickbase, ready for future writes.
- **Changing a dropdown:** add the value at the end, with its label exactly as Quickbase spells it. Never rename a value: its name is the API contract.

### Adding a supplier view

1. In `Infrastructure/Quickbase/SuppliersTable.cs`, add the field ids and a `…Fields` list in query order.
2. In `Application/Suppliers/SupplierContracts.cs`, add a DTO and a response record. Add a `Find…Async` method to `ISupplierSource`, then implement it in `QuickbaseSupplierSource`.
3. Add a `Get…Async` method to `SupplierService`. Its shared `ReadRecordAsync` does the access check, 404 and 502.
4. Add the action to both `SuppliersController` and `PlatformSuppliersController`.
5. Add a test with a real Quickbase response in `Fixtures/SupplierResponses.cs`, like `YardCapabilitiesSpecs`.

## Deployment

Pushes to `main` that touch `backend/` run [`cloudbuild.yaml`](cloudbuild.yaml):
build, test, container image, push to Artifact Registry, deploy to Cloud Run.
The infrastructure is defined in [`infra/terraform`](infra/terraform/README.md).

The Quickbase endpoints need `Quickbase__RealmHostname` and `Quickbase__UserToken`
on the service; keep the token in Secret Manager, not a plain env var.

**The currently deployed service is `wrigley-api`** (project `wrigley-cloud-prod`,
us-central1), deployed by hand. To ship the working copy:

```bash
cd backend
gcloud builds submit . --project=wrigley-cloud-prod \
  --tag=us-central1-docker.pkg.dev/wrigley-cloud-prod/wrigley-api/wrigley-api:<new tag>
gcloud run deploy wrigley-api --project=wrigley-cloud-prod --region=us-central1 \
  --image=us-central1-docker.pkg.dev/wrigley-cloud-prod/wrigley-api/wrigley-api:<new tag> \
  --update-env-vars=INTERNAL_HD_ALLOWLIST=scrapgo.com
curl -s https://wrigley-api-7iv6rka6zq-uc.a.run.app/healthz/ready
```

- **Image tags:** the repository already has tags up to `v12`, so use a new one each time (`v13`, `v14`, …). Never reuse a tag.
- **Migrations first:** apply them to the cloud database before deploying code that needs them.
