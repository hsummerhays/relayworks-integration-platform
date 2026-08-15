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
  type        = string
  description = "Entra app registration client id for the Control Plane API."
}
variable "control_plane_api_identifier_uri" {
  type        = string
  description = "Entra API Application ID / Identifier URI for the Control Plane API."
  default     = "api://relayworks-control-api-dev"
}
variable "console_client_id" {
  type        = string
  description = "Entra single-page application client id for the Vue console."
}
variable "alert_email" {
  type        = string
  description = "Operations email receiving RelayWorks Azure Monitor alerts."
}
variable "key_vault_admin_object_id" {
  type        = string
  description = "Entra principal object ID to grant Key Vault Secrets Officer role."
  default     = null
}
variable "connector_max_concurrency" {
  type        = number
  description = "Maximum concurrent destination calls for one connection profile."
  default     = 2
}
variable "connector_requests_per_second" {
  type        = number
  description = "Sustained destination request rate for one connection profile."
  default     = 5
}
variable "connector_burst_capacity" {
  type        = number
  description = "Token-bucket burst capacity for one connection profile."
  default     = 5
}
variable "archive_enabled" {
  type    = bool
  default = true
}
variable "archive_dry_run" {
  type        = bool
  description = "Discover archive candidates without uploading or deleting rows."
  default     = true
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
  default     = ["alerts@example.com"]
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
