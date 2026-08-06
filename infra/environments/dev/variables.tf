variable "subscription_id" {
  type        = string
  description = "Azure subscription receiving the RelayWorks dev resources."
}

variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant id."
}

variable "sql_entra_admin_login" {
  type        = string
  description = "Display name of the Entra principal administering Azure SQL."
}

variable "sql_entra_admin_object_id" {
  type        = string
  description = "Object id of the Entra principal administering Azure SQL."
}

variable "control_plane_image" {
  type        = string
  description = "Fully qualified Control Plane container image."
}

variable "sync_worker_image" {
  type        = string
  description = "Fully qualified Sync Worker container image."
}
variable "control_plane_api_client_id" {
  type = string
  description = "Entra app registration client id for the Control Plane API."
}
