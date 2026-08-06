module "relayworks" {
  source = "../../modules/relayworks-platform"

  name_prefix               = "relayworks"
  environment               = "dev"
  location                  = "westus2"
  tenant_id                 = var.tenant_id
  sql_entra_admin_login     = var.sql_entra_admin_login
  sql_entra_admin_object_id = var.sql_entra_admin_object_id
  control_plane_image       = var.control_plane_image
  sync_worker_image         = var.sync_worker_image
  control_plane_api_client_id = var.control_plane_api_client_id
  console_client_id           = var.console_client_id

  tags = {
    application = "relayworks"
    environment = "dev"
    managed-by  = "terraform"
  }
}
