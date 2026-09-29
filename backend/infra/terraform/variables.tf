variable "project_id" {
  description = "GCP project the environment is provisioned into (e.g. \"wrigley-cloud-prod\")."
  type        = string
}

variable "region" {
  description = "Region for every regional resource."
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Short environment name used as a resource-name suffix (e.g. \"dev\", \"prod\")."
  type        = string
}

variable "service_name" {
  description = "Base name for the Cloud Run service and related resources."
  type        = string
  default     = "scrapgo-core-api"
}

# --- Cloud Run ---

variable "container_image" {
  description = <<-EOT
    Image for the first deploy only. Cloud Build deploys every later image
    itself (cloudbuild.yaml), and cloudrun.tf ignores image drift, so this
    stays a bootstrap value. The default is Google's public hello-world
    image, so the service can be created before any image has been pushed.
  EOT
  type        = string
  default     = "us-docker.pkg.dev/cloudrun/container/hello"
}

variable "cloud_run_min_instances" {
  description = "Minimum instances. 0 allows scale-to-zero; use 1 or more for latency-sensitive environments."
  type        = number
  default     = 0
}

variable "cloud_run_max_instances" {
  description = "Maximum instances: a hard ceiling against runaway scale-out (and Cloud SQL connection exhaustion)."
  type        = number
  default     = 10
}

variable "public_ingress" {
  description = <<-EOT
    false (default): internal and load-balancer traffic only, with no public
    invoker. true: public HTTPS ingress, while the API still requires a valid
    GCIP bearer token on every protected route. Turn it on only when the
    frontend calls Cloud Run directly rather than through a load balancer.
  EOT
  type        = bool
  default     = false
}

# --- Cloud SQL ---

variable "database_tier" {
  description = "Cloud SQL machine tier."
  type        = string
  default     = "db-g1-small"
}

variable "database_name" {
  description = "Postgres database name."
  type        = string
  default     = "scrapgo_core"
}

variable "database_user" {
  description = "Postgres login the app connects as."
  type        = string
  default     = "scrapgo_core_app"
}

variable "database_proxy_access" {
  description = <<-EOT
    Give the instance a public IP with no authorized networks, reachable only
    through the Cloud SQL Auth Proxy, so developers can run the API locally
    against this database. Set false for a private-IP-only instance.
  EOT
  type        = bool
  default     = true
}

variable "database_proxy_users" {
  description = "IAM members allowed to connect through the Cloud SQL Auth Proxy (roles/cloudsql.client), e.g. [\"user:eric@scrapgo.com\"]."
  type        = list(string)
  default     = []
}

variable "database_deletion_protection" {
  description = "Protect the Cloud SQL instance from `terraform destroy`. Keep this true outside throwaway environments."
  type        = bool
  default     = true
}

# --- App configuration, surfaced to Cloud Run as environment variables (keys match appsettings.json) ---

variable "gcip_project_id" {
  description = "Google Cloud Identity Platform project whose ID tokens the API accepts (Gcip__ProjectId)."
  type        = string
}

variable "cors_allowed_origins" {
  description = "CORS allow-list (Cors__AllowedOrigins__N). Never a wildcard."
  type        = list(string)
  default     = []
}

variable "internal_hd_allowlist" {
  description = "Comma-separated Google Workspace hosted domains classified as Internal users (INTERNAL_HD_ALLOWLIST). Blank means everyone is External."
  type        = string
  default     = ""
}

variable "quickbase_realm_hostname" {
  description = "Quickbase realm (Quickbase__RealmHostname), e.g. \"scrapgo.quickbase.com\". Blank leaves Quickbase unconfigured."
  type        = string
  default     = ""
}

variable "quickbase_user_token_configured" {
  description = <<-EOT
    Set to true only after a version has been added to the Quickbase
    user-token secret (see README.md). Cloud Run refuses to start a revision
    that references a secret with no versions, so this switch gates the env var.
  EOT
  type        = bool
  default     = false
}

# --- Cloud Build GitHub connection ---
# All three come from the one-time manual GitHub App authorization described
# in cloudbuild.tf. None of them has a sensible default.

variable "github_repository_url" {
  description = "HTTPS clone URL of the repository Cloud Build watches, e.g. \"https://github.com/scrapgo/wrigley-web.git\"."
  type        = string
}

variable "github_app_installation_id" {
  description = "Installation id of the Google Cloud Build GitHub App on that repository."
  type        = string
}

variable "github_oauth_token_secret_version" {
  description = "Secret Manager secret version (full resource name) holding the GitHub OAuth token for the connection."
  type        = string
}

variable "deploy_branch_regex" {
  description = "Branches whose pushes build, test and deploy."
  type        = string
  default     = "^main$"
}
