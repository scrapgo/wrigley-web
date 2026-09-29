# Secret Manager secrets. Each one gets an IAM binding scoped to the Cloud
# Run runtime service account only, never a project-wide secretAccessor.
#
# The app reads a complete Npgsql connection string, so the secret payload is
# injected verbatim as ConnectionStrings__Default.

resource "google_secret_manager_secret" "db_connection_string" {
  secret_id = "${local.name}-db"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_connection_string" {
  secret      = google_secret_manager_secret.db_connection_string.id
  secret_data = "Host=${google_sql_database_instance.main.private_ip_address};Port=5432;Database=${google_sql_database.main.name};Username=${google_sql_user.app.name};Password=${random_password.app_db.result};SSL Mode=Require;Trust Server Certificate=true"
}

resource "google_secret_manager_secret_iam_member" "db_connection_string_accessor" {
  secret_id = google_secret_manager_secret.db_connection_string.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}

# The Quickbase user token is created empty on purpose: its value never passes
# through Terraform (and so never lands in state). Add it out of band:
#   printf '%s' "$TOKEN" | gcloud secrets versions add <name>-quickbase-token --data-file=-
# then set quickbase_user_token_configured = true.
resource "google_secret_manager_secret" "quickbase_user_token" {
  secret_id = "${local.name}-quickbase-token"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_iam_member" "quickbase_user_token_accessor" {
  secret_id = google_secret_manager_secret.quickbase_user_token.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}
