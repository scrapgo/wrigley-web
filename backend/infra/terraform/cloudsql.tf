# Cloud SQL Postgres 16, private IP only (no public IPv4). One database and
# one login. Schemas (identity, audit, quickbase) and their tables are
# created by the app's EF Core migrations, not by Terraform.

resource "random_password" "app_db" {
  length  = 32
  special = false
}

resource "google_sql_database_instance" "main" {
  name                = local.name
  database_version    = "POSTGRES_16"
  region              = var.region
  deletion_protection = var.database_deletion_protection

  depends_on = [google_service_networking_connection.private_service_access]

  settings {
    tier    = var.database_tier
    edition = "ENTERPRISE"

    ip_configuration {
      # Cloud Run always uses the private IP. A public IP exists only so
      # developers can connect from their machines through the Cloud SQL Auth
      # Proxy (IAM-authenticated, TLS). There are deliberately no authorized
      # networks, so nothing can connect to it directly.
      ipv4_enabled    = var.database_proxy_access
      private_network = google_compute_network.main.id
      ssl_mode        = "ENCRYPTED_ONLY"
    }

    backup_configuration {
      enabled                        = true
      point_in_time_recovery_enabled = true
    }
  }
}

resource "google_sql_database" "main" {
  name     = var.database_name
  instance = google_sql_database_instance.main.name
}

# Developers who may open a Cloud SQL Auth Proxy session. The proxy also
# needs the database login and password (from the connection-string secret).
resource "google_project_iam_member" "database_proxy_users" {
  for_each = var.database_proxy_access ? toset(var.database_proxy_users) : toset([])
  project  = var.project_id
  role     = "roles/cloudsql.client"
  member   = each.value
}

resource "google_sql_user" "app" {
  name     = var.database_user
  instance = google_sql_database_instance.main.name
  password = random_password.app_db.result
}
