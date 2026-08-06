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

variable "tags" {
  type = map(string)
}
