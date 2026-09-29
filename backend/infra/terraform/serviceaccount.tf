# The Cloud Run runtime identity. It holds only narrow, resource-scoped grants,
# declared next to the resources they apply to (secrets.tf). There is no
# roles/cloudsql.client: the service reaches Postgres over private IP via
# direct VPC egress, not the Cloud SQL Admin API or Auth Proxy.

resource "google_service_account" "cloud_run" {
  account_id   = "${var.service_name}-run" # <= 30 chars
  display_name = "ScrapGo Core API (${var.environment}) Cloud Run runtime"
}
