variable "subscription_id" {
  type = string
}
variable "location" {
  type    = string
  default = "westus2"
}
variable "storage_account_name" {
  type        = string
  description = "Globally unique lowercase storage account name."
}
