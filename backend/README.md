# ScrapGo Core API

The backend for the ScrapGo Downstream Portal: a .NET 10 modular monolith that
is the portal's **identity hub** (Google Cloud Identity Platform sign-in,
organizations, roles and permissions) and its **governed, cached proxy** to
Quickbase. The frontend talks only to this API, never to Quickbase.

- Architecture, rules and conventions: [`AGENTS.md`](AGENTS.md)
- GCP infrastructure and deployment: [`infra/terraform/README.md`](infra/terraform/README.md)

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

The frontend will start on http://localhost:5174 and connect to this API at http://localhost:5141. CORS is already configured to allow requests from the frontend's development server.

### 6. Call authenticated endpoints

Every `/api/**` endpoint needs a **Google Cloud Identity Platform ID token**
for project `wrigley-cloud-prod`, sent as `Authorization: Bearer <token>`.
Get one by signing in through the portal (or any GCIP client), then either:

- **Swagger:** click **Authorize** and paste the token without the `Bearer ` prefix.
- **VS Code / Visual Studio:** open [`src/ScrapGo.Core.Api/ScrapGo.Core.Api.http`](src/ScrapGo.Core.Api/ScrapGo.Core.Api.http), paste the token into `@token`, and send the requests.

Start with `GET /api/users/me`: the first call provisions your user record.

## Configuration

Non-secret development values live in `src/ScrapGo.Core.Api/appsettings.Development.json`.
Secrets live in `dotnet user-secrets` locally and Secret Manager in GCP;
**they never go in a committed file**. The full list, as environment variables,
is in [`.env.example`](.env.example).

| Setting                     | Required                | Local default           | Purpose                                            |
| --------------------------- | ----------------------- | ----------------------- | -------------------------------------------------- |
| `ConnectionStrings:Default` | Yes                     | user-secrets            | Postgres (Npgsql) connection string                |
| `Gcip:ProjectId`            | Yes                     | `wrigley-cloud-prod`    | GCIP project whose ID tokens are accepted          |
| `Quickbase:RealmHostname`   | On first Quickbase call | —                       | e.g. `scrapgo.quickbase.com`                       |
| `Quickbase:UserToken`       | On first Quickbase call | user-secrets            | Quickbase user token (secret)                      |
| `Quickbase:QueryCache:Ttl`  | No                      | `00:15:00`              | How long cached Quickbase results are served       |
| `Cors:AllowedOrigins`       | No                      | `http://localhost:3000` | Browser origins allowed to call the API            |
| `INTERNAL_HD_ALLOWLIST`     | No                      | —                       | Google Workspace domains treated as internal users |
| `Swagger:Enabled`           | No                      | `true`                  | Serves `/swagger` (off in Cloud Run unless set)    |

Add the Quickbase token the same way as the connection string:
`dotnet user-secrets set "Quickbase:UserToken" "<token>" --project src/ScrapGo.Core.Api`.

## Database migrations

Each module owns its own schema and migrations. With the proxy running, apply
them in this order (the audit schema first):

```bash
C="Host=127.0.0.1;Port=5434;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Disable"
dotnet ef database update --project src/Shared/ScrapGo.Core.Shared.Infrastructure --context AuditDbContext --connection "$C"
dotnet ef database update --project src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure --context IdentityDbContext --connection "$C"
dotnet ef database update --project src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure --context QuickbaseDbContext --connection "$C"
```

`dotnet ef` comes from `dotnet tool install --global dotnet-ef`. Only run
migrations against a database meant for this API: they create the `audit`,
`identity` and `quickbase` schemas.

## Tests

```bash
dotnet test ScrapGo.Core.slnx
```

The integration tests start their own throwaway Postgres containers
(Testcontainers), so **Docker must be running**, and they never touch Cloud SQL.
The full run takes about 12 minutes. While it runs, the test host locks the
test projects' DLLs, so don't `dotnet build` at the same time.

## Running in Docker

```bash
docker build -t scrapgo-core-api .
cp .env.example .env.local          # fill in; .env.local is git-ignored
docker run --rm -p 8080:8080 --env-file .env.local scrapgo-core-api
```

Inside the container, the proxy on your machine is `host.docker.internal:5434`
(already set in `.env.example`). The API listens on http://localhost:8080.

## Troubleshooting

| Symptom                                                                  | Cause / fix                                                                                                                        |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default is not configured` at startup                 | user-secrets not set: see step 1. Run from `backend/` with `--project src/ScrapGo.Core.Api`.                                       |
| `Gcip:ProjectId is not configured`                                       | Running with an environment other than Development and no `Gcip__ProjectId` set.                                                   |
| `/healthz/ready` returns `503`                                           | The proxy isn't running, is on another port, or the credentials are wrong.                                                         |
| Proxy: `could not find default credentials`                              | Run `gcloud auth application-default login`.                                                                                       |
| Proxy can't connect to the instance                                      | The instance has no proxy-reachable IP (private-IP only). See the infra README.                                                    |
| Every `/api/**` call returns `401`                                       | Missing or expired token (tokens last at most 1 hour), or it was issued for another GCIP project.                                  |
| `403` with `reason: user_disabled`                                       | Your user record is disabled.                                                                                                      |
| `dotnet build` fails with "file is locked by testhost"                   | A test run is still going. Wait for it or stop it.                                                                                 |
| `401` and the request shows `Authorization: Bearer "@token` (or similar) | A placeholder was pasted into Swagger's **Authorize** box. Paste the raw ID token (`eyJ…`) with no quotes and no `Bearer ` prefix. |

## Admin API roadmap

The admin portal needs endpoints this API does not expose yet (user listing,
role listing and read-back, role assignment, user status, organization and
membership management), plus a fix for `Admin.Access` never being granted.
The complete gap list and suggested implementation order are in
[`ADMIN-API-GAPS.md`](ADMIN-API-GAPS.md).

## Deployment

Pushes to `main` that touch `backend/` run [`cloudbuild.yaml`](cloudbuild.yaml):
build, test, container image, push to Artifact Registry, deploy to Cloud Run.
The infrastructure is defined in [`infra/terraform`](infra/terraform/README.md).
