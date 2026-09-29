# A private VPC for Cloud SQL, plus the private-services peering range it
# needs. Cloud Run reaches the VPC via direct VPC egress (cloudrun.tf), so
# no Serverless VPC Access connector is required.

resource "google_compute_network" "main" {
  name                    = local.name
  auto_create_subnetworks = false
}

resource "google_compute_subnetwork" "main" {
  name          = local.name
  ip_cidr_range = "10.20.0.0/24"
  region        = var.region
  network       = google_compute_network.main.id

  # Required for Cloud Run direct VPC egress from this subnet.
  private_ip_google_access = true
}

# Reserved range for VPC peering with Google-managed services (Cloud SQL
# private IP). A later Memorystore instance can share it.
resource "google_compute_global_address" "private_service_access" {
  name          = "${local.name}-psa"
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = 20
  network       = google_compute_network.main.id
}

resource "google_service_networking_connection" "private_service_access" {
  network                 = google_compute_network.main.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_service_access.name]
}
