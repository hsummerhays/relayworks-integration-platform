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
variable "control_plane_api_client_id" {
  type = string
}
variable "control_plane_api_identifier_uri" {
  type        = string
  description = "Entra API Application ID / Identifier URI for the Control Plane API."
  default     = "https://hsummerhays1gmail.onmicrosoft.com/relayworks-control-api-dev"
}
variable "console_client_id" {
  type = string
}
variable "alert_email" {
  type = string
}
variable "key_vault_admin_object_id" {
  type        = string
  description = "Entra principal object ID to grant Key Vault Secrets Officer role."
  default     = null
}
variable "connector_max_concurrency" {
  type    = number
  default = 2
}
variable "connector_requests_per_second" {
  type    = number
  default = 5
}
variable "connector_burst_capacity" {
  type    = number
  default = 5
}
variable "archive_enabled" {
  type    = bool
  default = true
}
variable "archive_dry_run" {
  type    = bool
  default = true
}

variable "log_analytics_daily_quota_gb" {
  type        = number
  description = "The daily ingestion quota for the Log Analytics workspace in GB."
  default     = 0.1
}

variable "archive_account_replication_type" {
  type        = string
  description = "The replication type for the archive storage account (e.g., LRS, ZRS)."
  default     = "LRS"
}

variable "control_plane_min_replicas" {
  type        = number
  description = "Minimum replicas for the Control Plane Container App."
  default     = 0
}

variable "control_plane_max_replicas" {
  type        = number
  description = "Maximum replicas for the Control Plane Container App."
  default     = 3
}

variable "sync_worker_min_replicas" {
  type        = number
  description = "Minimum replicas for the Sync Worker Container App."
  default     = 0
}

variable "sync_worker_max_replicas" {
  type        = number
  description = "Maximum replicas for the Sync Worker Container App."
  default     = 1
}

variable "monthly_budget_amount" {
  type        = number
  description = "Monthly budget limit in USD."
  default     = 50
}

variable "budget_contact_emails" {
  type        = list(string)
  description = "List of email addresses to receive budget threshold alerts."
  default     = ["hsummerhays1@gmail.com"]
}

variable "deploy_applications" {
  type        = bool
  description = "Whether to deploy the Control Plane and Worker Container Apps."
  default     = false
}

variable "budget_start_date" {
  type        = string
  description = "Start date for the monthly budget period in RFC3339 format."
  default     = "2026-08-01T00:00:00Z"
}

variable "migration_image" {
  type        = string
  description = "Fully qualified container image for the database migration job."
  default     = "acrrelayworksdev.azurecr.io/relayworks-migrations@sha256:5343b17a7741c4d1e2b0231c7624e88cab8b4e1b573b9914f0ecc2cf63a8d3f1"
}

variable "tags" {
  type = map(string)
}
