# Provider and version pinning, so `terraform plan` is reproducible across
# machines.
#
# There is no backend block on purpose: state location is chosen per
# environment with `terraform init -backend-config=...` (see README.md).
# State contains the generated database password, so it must live in a
# locked-down GCS bucket, never in git.
terraform {
  required_version = ">= 1.5.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

locals {
  # e.g. "scrapgo-core-api-dev". Every resource is suffixed by environment,
  # so environments can share a project without colliding. It also never
  # collides with the legacy identity-platform-* resources.
  name = "${var.service_name}-${var.environment}"
}
