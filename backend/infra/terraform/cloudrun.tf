# The Cloud Run service. Env var names match the app's configuration keys
# (appsettings.json, with "__" for ":"). Secrets are injected from Secret
# Manager, never as plain values.

resource "google_cloud_run_v2_service" "main" {
  name     = local.name
  location = var.region
  ingress  = var.public_ingress ? "INGRESS_TRAFFIC_ALL" : "INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"

  deletion_protection = false

  template {
    service_account = google_service_account.cloud_run.email

    scaling {
      min_instance_count = var.cloud_run_min_instances
      max_instance_count = var.cloud_run_max_instances
    }

    vpc_access {
      network_interfaces {
        network    = google_compute_network.main.id
        subnetwork = google_compute_subnetwork.main.id
      }
      # Only private ranges (Cloud SQL) go through the VPC; Quickbase, GCIP
      # JWKS and other public APIs egress directly.
      egress = "PRIVATE_RANGES_ONLY"
    }

    containers {
      image = var.container_image

      ports {
        container_port = 8080
      }

      startup_probe {
        http_get {
          path = "/healthz"
        }
        initial_delay_seconds = 2
        period_seconds        = 5
        failure_threshold     = 12
      }

      liveness_probe {
        http_get {
          path = "/healthz"
        }
        period_seconds = 30
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "Gcip__ProjectId"
        value = var.gcip_project_id
      }

      env {
        name  = "INTERNAL_HD_ALLOWLIST"
        value = var.internal_hd_allowlist
      }

      dynamic "env" {
        for_each = { for i, origin in var.cors_allowed_origins : i => origin }
        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }

      env {
        name = "ConnectionStrings__Default"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_connection_string.secret_id
            version = "latest"
          }
        }
      }

      dynamic "env" {
        for_each = var.quickbase_realm_hostname != "" ? [var.quickbase_realm_hostname] : []
        content {
          name  = "Quickbase__RealmHostname"
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.quickbase_user_token_configured ? [1] : []
        content {
          name = "Quickbase__UserToken"
          value_source {
            secret_key_ref {
              secret  = google_secret_manager_secret.quickbase_user_token.secret_id
              version = "latest"
            }
          }
        }
      }
    }
  }

  lifecycle {
    # Cloud Build deploys new images on every push; without this, the next
    # `terraform apply` would roll the service back to var.container_image.
    ignore_changes = [
      template[0].containers[0].image,
      client,
      client_version,
    ]
  }

  depends_on = [
    google_secret_manager_secret_version.db_connection_string,
    google_secret_manager_secret_iam_member.db_connection_string_accessor,
    google_secret_manager_secret_iam_member.quickbase_user_token_accessor,
  ]
}

# Public invocation only when explicitly enabled. The API still authenticates
# every protected route with a GCIP bearer token.
resource "google_cloud_run_v2_service_iam_member" "public_invoker" {
  count    = var.public_ingress ? 1 : 0
  name     = google_cloud_run_v2_service.main.name
  location = google_cloud_run_v2_service.main.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}
