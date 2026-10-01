resource "azurerm_mssql_server" "main" {
  name                         = "sql-${var.prefix}-${var.suffix}"
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  minimum_tls_version          = "1.2"
  administrator_login          = var.administrator_login
  administrator_login_password = var.administrator_password

  azuread_administrator {
    login_username = var.aad_admin_login
    object_id      = var.aad_admin_object_id
    tenant_id      = var.aad_admin_tenant_id
  }
}

resource "azapi_resource" "database" {
  type      = "Microsoft.Sql/servers/databases@2024-11-01-preview"
  name      = "sqldb-${var.prefix}"
  parent_id = azurerm_mssql_server.main.id
  location  = var.location

  body = {
    sku = {
      name     = "GP_S_Gen5"
      tier     = "GeneralPurpose"
      family   = "Gen5"
      capacity = 2
    }
    properties = {
      minCapacity                      = 0.5
      autoPauseDelay                   = 60
      maxSizeBytes                     = 32 * 1024 * 1024 * 1024
      useFreeLimit                     = true
      freeLimitExhaustionBehavior      = "AutoPause"
      zoneRedundant                    = false
      requestedBackupStorageRedundancy = "Local"
    }
  }
}

resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "owner" {
  count            = var.owner_ip_address == "" ? 0 : 1
  name             = "Owner"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = var.owner_ip_address
  end_ip_address   = var.owner_ip_address
}
