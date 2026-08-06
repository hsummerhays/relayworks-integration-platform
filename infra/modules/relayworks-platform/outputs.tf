output "container_registry_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "control_plane_url" {
  value = "https://${azurerm_container_app.control_plane.ingress[0].fqdn}"
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
