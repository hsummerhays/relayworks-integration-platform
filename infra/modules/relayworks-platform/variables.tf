variable "name_prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "environment" {
  type = string
}

variable "tenant_id" {
  type = string
}

variable "sql_entra_admin_login" {
  type = string
}

variable "sql_entra_admin_object_id" {
  type = string
}

variable "control_plane_image" {
  type = string
}

variable "sync_worker_image" {
  type = string
}
variable "control_plane_api_client_id" { type = string }
variable "console_client_id" { type = string }
variable "alert_email" { type = string }
variable "connector_max_concurrency" { type = number default = 2 }
variable "connector_requests_per_second" { type = number default = 5 }
variable "connector_burst_capacity" { type = number default = 5 }
variable "archive_enabled" { type = bool default = true }
variable "archive_dry_run" { type = bool default = true }

variable "tags" {
  type = map(string)
}
