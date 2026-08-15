module "relayworks" {
  source = "../../modules/relayworks-platform"

  name_prefix                      = "relayworks"
  environment                      = "dev"
  location                         = "westus2"
  tenant_id                        = var.tenant_id
  sql_entra_admin_login            = var.sql_entra_admin_login
  sql_entra_admin_object_id        = var.sql_entra_admin_object_id
  control_plane_image              = var.control_plane_image
  sync_worker_image                = var.sync_worker_image
  control_plane_api_client_id      = var.control_plane_api_client_id
  control_plane_api_identifier_uri = var.control_plane_api_identifier_uri
  console_client_id                = var.console_client_id
  alert_email                      = var.alert_email
  key_vault_admin_object_id        = var.key_vault_admin_object_id
  connector_max_concurrency        = var.connector_max_concurrency
  connector_requests_per_second    = var.connector_requests_per_second
  connector_burst_capacity         = var.connector_burst_capacity
  archive_enabled                  = var.archive_enabled
  archive_dry_run                  = var.archive_dry_run
  log_analytics_daily_quota_gb     = var.log_analytics_daily_quota_gb
  archive_account_replication_type = var.archive_account_replication_type
  control_plane_min_replicas       = var.control_plane_min_replicas
  control_plane_max_replicas       = var.control_plane_max_replicas
  sync_worker_min_replicas         = var.sync_worker_min_replicas
  sync_worker_max_replicas         = var.sync_worker_max_replicas
  monthly_budget_amount            = var.monthly_budget_amount
  budget_contact_emails            = var.budget_contact_emails
  deploy_applications              = var.deploy_applications
  budget_start_date                = var.budget_start_date
  migration_image                  = var.migration_image

  tags = {
    environment = "dev"
    project     = "RelayWorks"
    owner       = "Hugh"
    managed-by  = "terraform"
  }
}
