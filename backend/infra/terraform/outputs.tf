output "cloud_run_service_name" {
  value = google_cloud_run_v2_service.main.name
}

output "cloud_run_service_url" {
  description = "Service URL. With public_ingress = false it is reachable only internally or through a load balancer."
  value       = google_cloud_run_v2_service.main.uri
}

output "cloud_run_service_account_email" {
  value = google_service_account.cloud_run.email
}

output "artifact_registry_repository" {
  value = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.images.repository_id}"
}

output "cloud_sql_instance_connection_name" {
  value = google_sql_database_instance.main.connection_name
}

output "cloud_sql_private_ip_address" {
  value = google_sql_database_instance.main.private_ip_address
}

output "db_connection_string_secret" {
  value = google_secret_manager_secret.db_connection_string.secret_id
}

output "quickbase_user_token_secret" {
  description = "Add the token with: printf '%s' \"$TOKEN\" | gcloud secrets versions add <this> --data-file=-"
  value       = google_secret_manager_secret.quickbase_user_token.secret_id
}
