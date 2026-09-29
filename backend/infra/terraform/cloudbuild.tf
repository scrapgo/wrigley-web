# The Cloud Build trigger that runs backend/cloudbuild.yaml on pushes to
# deploy_branch_regex, and only when something under backend/ changed (this is
# a monorepo with the frontend alongside).
#
# The GitHub connection needs one interactive step outside Terraform:
# authorizing the Google Cloud Build GitHub App on the repository (Cloud
# Console -> Cloud Build -> Repositories -> Connect Repository). The first
# apply in a project pauses for it. The resources below capture the result.

resource "google_cloudbuildv2_connection" "github" {
  location = var.region
  name     = "${local.name}-github"

  github_config {
    app_installation_id = var.github_app_installation_id
    authorizer_credential {
      oauth_token_secret_version = var.github_oauth_token_secret_version
    }
  }
}

resource "google_cloudbuildv2_repository" "main" {
  location          = var.region
  name              = local.name
  parent_connection = google_cloudbuildv2_connection.github.name
  remote_uri        = var.github_repository_url
}

resource "google_cloudbuild_trigger" "on_push" {
  location = var.region
  name     = "${local.name}-on-push"

  repository_event_config {
    repository = google_cloudbuildv2_repository.main.id
    push {
      branch = var.deploy_branch_regex
    }
  }

  filename       = "backend/cloudbuild.yaml"
  included_files = ["backend/**"]

  substitutions = {
    _REGION       = var.region
    _SERVICE_NAME = google_cloud_run_v2_service.main.name
    _REPOSITORY   = google_artifact_registry_repository.images.repository_id
  }
}
