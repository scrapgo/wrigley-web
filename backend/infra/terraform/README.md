# ScrapGo Core API: GCP infrastructure (Terraform)

Provisions one environment of the API in a GCP project:

| Resource | File | Notes |
|---|---|---|
| VPC, subnet, private-services peering | `network.tf` | Cloud Run reaches Cloud SQL on its private IP |
| Cloud SQL Postgres 16 | `cloudsql.tf` | Encrypted-only; backups and PITR on; one DB and one login. Optional proxy-only public IP (below) |
| Artifact Registry (Docker) | `artifactregistry.tf` | Keeps the 20 newest images |
| Secret Manager | `secrets.tf` | DB connection string (generated), Quickbase user token (added by hand) |
| Cloud Run runtime service account | `serviceaccount.tf` | Only per-secret accessor grants |
| Cloud Run service | `cloudrun.tf` | Direct VPC egress, `/healthz` probes, env vars = app config keys |
| Cloud Build GitHub trigger | `cloudbuild.tf` | Runs `backend/cloudbuild.yaml` on pushes touching `backend/` |

Names are `scrapgo-core-api-<environment>`, so this never collides with the
legacy `identity-platform-*` resources in the same project. The legacy state
(`identity-platform/terraform/terraform.tfstate`) was deliberately **not**
migrated: this is a separate environment with its own state.

## Before the first apply

1. Enable the APIs: `run`, `sqladmin`, `servicenetworking`, `compute`,
   `secretmanager`, `artifactregistry`, `cloudbuild`.
2. Create a GCS bucket for state (versioned, uniform access, no public access).
   State contains the generated DB password.
3. Connect the GitHub repository to Cloud Build once, interactively (Cloud
   Console → Cloud Build → Repositories), and note the app installation id and
   the OAuth-token secret version for the tfvars.

## Apply

```bash
cp terraform.tfvars.example environments/dev.tfvars   # git-ignored; fill in
terraform init -backend-config="bucket=<state-bucket>" -backend-config="prefix=scrapgo-core-api/dev"
terraform plan  -var-file=environments/dev.tfvars
terraform apply -var-file=environments/dev.tfvars
```

The first apply deploys Google's hello-world image (the `container_image`
default), because no API image exists yet. The first Cloud Build run on
`main` replaces it. Cloud Run ignores later image drift, so `terraform apply`
never rolls back a deployed image.

## Connecting from a developer machine (Cloud SQL Auth Proxy)

With `database_proxy_access = true` (the default), the instance gets a
public IP with **no authorized networks**: only the Cloud SQL Auth Proxy
(IAM-authenticated, TLS) can reach it. List developers in
`database_proxy_users` to grant `roles/cloudsql.client`.

```bash
gcloud auth application-default login
cloud-sql-proxy "$(terraform output -raw cloud_sql_instance_connection_name)" --port 5434
# Then point the API at it (outside the repo, in user-secrets):
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=127.0.0.1;Port=5434;Database=scrapgo_core;Username=scrapgo_core_app;Password=<from the db secret>;SSL Mode=Disable" \
  --project src/ScrapGo.Core.Api
```

Read the password from the `…-db` secret: `gcloud secrets versions access latest --secret "$(terraform output -raw db_connection_string_secret)"`.

## After the first apply

- **Quickbase token.** The secret is created empty; its value never passes
  through Terraform state:
  ```bash
  printf '%s' "$QB_TOKEN" | gcloud secrets versions add "$(terraform output -raw quickbase_user_token_secret)" --data-file=-
  ```
  Then set `quickbase_user_token_configured = true` and apply again.
- **Database migrations.** Run them through the proxy from a developer
  machine, in order: `AuditDbContext`, `IdentityDbContext`,
  `QuickbaseDbContext` (commands in `backend/AGENTS.md`).
- **Ingress.** The default `public_ingress = false` accepts only internal and
  load-balancer traffic. Set it to true only if the frontend calls Cloud Run
  directly; the API still requires a GCIP bearer token on protected routes.

## Deliberately not included yet

Memorystore Redis (the permission cache is a pass-through, and there is no
rate limiter yet), the audit export bucket and job, and an HTTPS load
balancer with Cloud Armor. Add them when the modules that use them are migrated.
