data "azurerm_client_config" "current" {}

locals {
  name = "${var.name_prefix}-${var.environment}"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = var.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = var.log_analytics_daily_quota_gb
  tags                = var.tags
}

resource "azurerm_consumption_budget_resource_group" "main" {
  name              = "budget-${local.name}"
  resource_group_id = azurerm_resource_group.main.id

  amount     = var.monthly_budget_amount
  time_grain = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  notification {
    enabled        = true
    threshold      = 50.0
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.budget_contact_emails
  }

  notification {
    enabled        = true
    threshold      = 75.0
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.budget_contact_emails
  }

  notification {
    enabled        = true
    threshold      = 90.0
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.budget_contact_emails
  }

  notification {
    enabled        = true
    threshold      = 100.0
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.budget_contact_emails
  }
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = var.tags
}

resource "azurerm_monitor_action_group" "operations" {
  name                = "ag-${local.name}-operations"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "relayworks"
  tags                = var.tags
  email_receiver {
    name          = "operations"
    email_address = var.alert_email
  }
}

resource "azurerm_monitor_metric_alert" "service_bus_dead_letters" {
  name                = "alert-${local.name}-servicebus-deadletters"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_servicebus_namespace.main.id]
  description         = "Any dead-lettered RelayWorks message requires operator review."
  severity            = 1
  frequency           = "PT5M"
  window_size         = "PT5M"
  criteria {
    metric_namespace = "Microsoft.ServiceBus/namespaces"
    metric_name      = "DeadletteredMessages"
    aggregation      = "Maximum"
    operator         = "GreaterThan"
    threshold        = 0
  }
  action { action_group_id = azurerm_monitor_action_group.operations.id }
  tags = var.tags
}

resource "azurerm_monitor_metric_alert" "application_exceptions" {
  name                = "alert-${local.name}-application-exceptions"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "RelayWorks emitted application exceptions in the last five minutes."
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT5M"
  criteria {
    metric_namespace = "Microsoft.Insights/components"
    metric_name      = "exceptions/count"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 0
  }
  action { action_group_id = azurerm_monitor_action_group.operations.id }
  tags = var.tags
}

resource "azurerm_static_web_app" "console" {
  name                = "swa-${local.name}-console"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = var.tags
}

resource "azurerm_storage_account" "archive" {
  name                            = substr(replace("st${local.name}archive", "-", ""), 0, 24)
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = var.archive_account_replication_type
  min_tls_version                 = "TLS1_2"
  public_network_access_enabled   = false
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
  tags                            = var.tags
  blob_properties {
    versioning_enabled = true
    delete_retention_policy { days = 30 }
    container_delete_retention_policy { days = 30 }
  }
}

resource "azurerm_storage_container" "history" {
  name                  = "integration-history"
  storage_account_id    = azurerm_storage_account.archive.id
  container_access_type = "private"
}

resource "azurerm_storage_management_policy" "archive" {
  storage_account_id = azurerm_storage_account.archive.id
  rule {
    name    = "integration-history-lifecycle"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["integration-history/tenant="]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than    = 30
        tier_to_archive_after_days_since_modification_greater_than = 90
        delete_after_days_since_modification_greater_than          = 2555
      }
      snapshot { delete_after_days_since_creation_greater_than = 90 }
      version { delete_after_days_since_creation = 90 }
    }
  }
}

resource "azurerm_private_dns_zone" "blob" {
  name                = "privatelink.blob.core.windows.net"
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "blob" {
  name                  = "blob-vnet-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.blob.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_endpoint" "archive_blob" {
  name                = "pe-${local.name}-archive-blob"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags
  private_service_connection {
    name                           = "archive-blob-private-connection"
    private_connection_resource_id = azurerm_storage_account.archive.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }
  private_dns_zone_group {
    name                 = "archive-blob-private-dns"
    private_dns_zone_ids = [azurerm_private_dns_zone.blob.id]
  }
}

resource "azurerm_container_registry" "main" {
  name                = replace("acr${local.name}", "-", "")
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.name}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  infrastructure_subnet_id   = azurerm_subnet.container_apps.id
  tags                       = var.tags
}

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.name}"
  address_space       = ["10.42.0.0/16"]
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_subnet" "container_apps" {
  name                 = "snet-container-apps"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.42.0.0/23"]

  delegation {
    name = "container-app-environments"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "private_endpoints" {
  name                 = "snet-private-endpoints"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.42.2.0/24"]
}

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Standard"
  local_auth_enabled  = false
  tags                = var.tags
}

resource "azurerm_servicebus_queue" "commands" {
  name                                    = "integration-commands"
  namespace_id                            = azurerm_servicebus_namespace.main.id
  requires_duplicate_detection            = true
  duplicate_detection_history_time_window = "PT10M"
  dead_lettering_on_message_expiration    = true
  max_delivery_count                      = 10
}

resource "azurerm_servicebus_topic" "events" {
  name                                    = "integration-events"
  namespace_id                            = azurerm_servicebus_namespace.main.id
  requires_duplicate_detection            = true
  duplicate_detection_history_time_window = "PT10M"
}

resource "azurerm_servicebus_subscription" "control_plane" {
  name               = "control-plane"
  topic_id           = azurerm_servicebus_topic.events.id
  max_delivery_count = 10
}

resource "azurerm_mssql_server" "main" {
  name                          = "sql-${local.name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = false
  tags                          = var.tags

  azuread_administrator {
    login_username              = var.sql_entra_admin_login
    object_id                   = var.sql_entra_admin_object_id
    tenant_id                   = var.tenant_id
    azuread_authentication_only = true
  }
}

resource "azurerm_private_dns_zone" "sql" {
  name                = "privatelink.database.windows.net"
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "sql" {
  name                  = "sql-vnet-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.sql.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_endpoint" "sql" {
  name                = "pe-${local.name}-sql"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = var.tags

  private_service_connection {
    name                           = "sql-private-connection"
    private_connection_resource_id = azurerm_mssql_server.main.id
    subresource_names              = ["sqlServer"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "sql-private-dns"
    private_dns_zone_ids = [azurerm_private_dns_zone.sql.id]
  }
}

resource "azurerm_mssql_database" "control_plane" {
  name                        = "relayworks-control"
  server_id                   = azurerm_mssql_server.main.id
  sku_name                    = "GP_S_Gen5_1"
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 32
  zone_redundant              = false
  tags                        = var.tags
}

resource "azurerm_mssql_database" "worker_ledger" {
  name                        = "relayworks-worker"
  server_id                   = azurerm_mssql_server.main.id
  sku_name                    = "GP_S_Gen5_1"
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 32
  zone_redundant              = false
  tags                        = var.tags
}

resource "azurerm_key_vault" "main" {
  name                       = substr(replace("kv-${local.name}", "-", ""), 0, 24)
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  purge_protection_enabled   = true
  soft_delete_retention_days = 7
  tags                       = var.tags
}

resource "azurerm_user_assigned_identity" "control_plane" {
  name                = "id-${local.name}-control"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "sync_worker" {
  name                = "id-${local.name}-worker"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_role_assignment" "control_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.control_plane.principal_id
}

resource "azurerm_role_assignment" "worker_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.sync_worker.principal_id
}

resource "azurerm_role_assignment" "control_servicebus_sender" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_user_assigned_identity.control_plane.principal_id
}

resource "azurerm_role_assignment" "control_servicebus_receiver" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_user_assigned_identity.control_plane.principal_id
}

resource "azurerm_role_assignment" "control_archive_blob" {
  scope                = azurerm_storage_account.archive.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.control_plane.principal_id
}

resource "azurerm_role_assignment" "worker_servicebus_sender" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_user_assigned_identity.sync_worker.principal_id
}

resource "azurerm_role_assignment" "worker_servicebus_receiver" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_user_assigned_identity.sync_worker.principal_id
}

resource "azurerm_role_assignment" "worker_key_vault_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.sync_worker.principal_id
}

resource "azurerm_role_assignment" "key_vault_admin" {
  count                = var.key_vault_admin_object_id != null ? 1 : 0
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.key_vault_admin_object_id
}

resource "azurerm_user_assigned_identity" "migrations" {
  name                = "id-${local.name}-migrations"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_role_assignment" "migrations_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.migrations.principal_id
}

resource "azurerm_container_app_job" "migrations" {
  name                         = "caj-${local.name}-migrations"
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  replica_timeout_in_seconds   = 1800
  replica_retry_limit          = 0
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.migrations.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.migrations.id
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "migrations"
      image  = var.migration_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.migrations.client_id
      }
      env {
        name  = "ConnectionStrings__RelayWorks"
        value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.control_plane.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False"
      }
      env {
        name  = "ConnectionStrings__WorkerLedger"
        value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.worker_ledger.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False"
      }
      env {
        name  = "SqlBootstrap__ControlPlanePrincipalName"
        value = azurerm_user_assigned_identity.control_plane.name
      }
      env {
        name  = "SqlBootstrap__ControlPlanePrincipalObjectId"
        value = azurerm_user_assigned_identity.control_plane.principal_id
      }
      env {
        name  = "SqlBootstrap__WorkerPrincipalName"
        value = azurerm_user_assigned_identity.sync_worker.name
      }
      env {
        name  = "SqlBootstrap__WorkerPrincipalObjectId"
        value = azurerm_user_assigned_identity.sync_worker.principal_id
      }
    }
  }
}

resource "azurerm_container_app" "control_plane" {
  count                        = var.deploy_applications ? 1 : 0
  name                         = "ca-${local.name}-control"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.control_plane.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.control_plane.id
  }

  template {
    min_replicas = var.control_plane_min_replicas
    max_replicas = var.control_plane_max_replicas
    container {
      name   = "control-plane"
      image  = var.control_plane_image
      cpu    = 0.5
      memory = "1Gi"
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.control_plane.client_id
      }
      env {
        name  = "ServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
      }
      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }
      env {
        name  = "ConnectionStrings__RelayWorks"
        value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.control_plane.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False"
      }
      env {
        name  = "Cors__AllowedOrigins__0"
        value = "https://${azurerm_static_web_app.console.default_host_name}"
      }
      env {
        name  = "Authentication__Enabled"
        value = "true"
      }
      env {
        name  = "AzureAd__TenantId"
        value = var.tenant_id
      }
      env {
        name  = "AzureAd__ClientId"
        value = var.control_plane_api_client_id
      }
      env {
        name  = "AzureAd__Audience"
        value = var.control_plane_api_identifier_uri
      }
      env {
        name  = "Archive__Enabled"
        value = tostring(var.archive_enabled)
      }
      env {
        name  = "Archive__DryRun"
        value = tostring(var.archive_dry_run)
      }
      env {
        name  = "Archive__BlobServiceUri"
        value = azurerm_storage_account.archive.primary_blob_endpoint
      }

      liveness_probe {
        transport        = "HTTP"
        port             = 8080
        path             = "/health"
        interval_seconds = 30
      }

      readiness_probe {
        transport        = "HTTP"
        port             = 8080
        path             = "/health/ready"
        interval_seconds = 10
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}

resource "azurerm_container_app" "sync_worker" {
  count                        = var.deploy_applications ? 1 : 0
  name                         = "ca-${local.name}-worker"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.sync_worker.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.sync_worker.id
  }

  template {
    min_replicas = var.sync_worker_min_replicas
    # The connection-scoped limiter is replica-local. Keep one Worker replica until
    # a distributed lease/token store coordinates destination quotas across replicas.
    max_replicas = var.sync_worker_max_replicas

    custom_scale_rule {
      name             = "service-bus-queue-scale"
      custom_rule_type = "azure-servicebus"
      identity_id      = azurerm_user_assigned_identity.sync_worker.id
      metadata = {
        queueName    = azurerm_servicebus_queue.commands.name
        namespace    = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
        messageCount = "1"
      }
    }

    container {
      name   = "sync-worker"
      image  = var.sync_worker_image
      cpu    = 0.5
      memory = "1Gi"
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.sync_worker.client_id
      }
      env {
        name  = "ServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
      }
      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }
      env {
        name  = "ConnectionStrings__WorkerLedger"
        value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.worker_ledger.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False"
      }
      env {
        name  = "ConnectorResilience__MaxConcurrentRequestsPerConnection"
        value = tostring(var.connector_max_concurrency)
      }
      env {
        name  = "ConnectorResilience__RequestsPerSecondPerConnection"
        value = tostring(var.connector_requests_per_second)
      }
      env {
        name  = "ConnectorResilience__BurstCapacityPerConnection"
        value = tostring(var.connector_burst_capacity)
      }
      env {
        name  = "Retention__Enabled"
        value = "true"
      }
      env {
        name  = "Retention__DryRun"
        value = tostring(var.archive_dry_run)
      }
    }
  }
}
