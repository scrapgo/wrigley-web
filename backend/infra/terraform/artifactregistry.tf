# Docker repository Cloud Build pushes the API image to:
#   {region}-docker.pkg.dev/{project}/{name}/scrapgo-core-api:{sha}

resource "google_artifact_registry_repository" "images" {
  location      = var.region
  repository_id = local.name
  format        = "DOCKER"
  description   = "ScrapGo Core API images (${var.environment})."

  # Keep the 20 most recent images; prune the rest.
  cleanup_policies {
    id     = "keep-recent"
    action = "KEEP"
    most_recent_versions {
      keep_count = 20
    }
  }

  cleanup_policies {
    id     = "delete-old"
    action = "DELETE"
    condition {
      older_than = "2592000s" # 30 days
    }
  }
}
