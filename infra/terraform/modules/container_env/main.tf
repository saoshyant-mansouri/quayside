resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${var.prefix}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_workspace_id
}
