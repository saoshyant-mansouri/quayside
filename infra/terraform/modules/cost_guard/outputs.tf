output "action_group_id" {
  value = azurerm_monitor_action_group.cost_guard.id
}

output "budget_name" {
  value = azurerm_consumption_budget_resource_group.main.name
}
