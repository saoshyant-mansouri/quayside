output "fqdn" {
  value = azurerm_container_app.api.ingress[0].fqdn
}

output "name" {
  value = azurerm_container_app.api.name
}

output "container_app_id" {
  value = azurerm_container_app.api.id
}

output "identity_id" {
  value = azurerm_user_assigned_identity.api.id
}

output "identity_client_id" {
  value = azurerm_user_assigned_identity.api.client_id
}
