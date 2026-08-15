output "container_registry_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "control_plane_url" {
  value = var.deploy_applications ? "https://${azurerm_container_app.control_plane[0].ingress[0].fqdn}" : null
}

output "console_url" {
  value = "https://${azurerm_static_web_app.console.default_host_name}"
}

output "service_bus_namespace" {
  value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "worker_ledger_database" {
  value = azurerm_mssql_database.worker_ledger.name
}

output "application_insights_name" {
  value = azurerm_application_insights.main.name
}

output "operations_action_group_name" {
  value = azurerm_monitor_action_group.operations.name
}

output "archive_storage_account_name" {
  value = azurerm_storage_account.archive.name
}

output "console_auth_build_settings" {
  value = {
    VITE_AUTH_ENABLED    = "true"
    VITE_ENTRA_TENANT_ID = var.tenant_id
    VITE_ENTRA_CLIENT_ID = var.console_client_id
    VITE_API_SCOPE       = "${var.control_plane_api_identifier_uri}/access_as_user"
    VITE_API_BASE_URL    = var.deploy_applications ? "https://${azurerm_container_app.control_plane[0].ingress[0].fqdn}" : null
  }
}
