resource "azurerm_monitor_action_group" "cost_guard" {
  name                = "ag-${var.prefix}-costguard"
  resource_group_name = var.resource_group_name
  short_name          = "costguard"

  dynamic "email_receiver" {
    for_each = var.alert_emails
    content {
      name          = "owner-${email_receiver.key}"
      email_address = email_receiver.value
    }
  }
}

resource "azurerm_consumption_budget_resource_group" "main" {
  name              = "budget-${var.prefix}"
  resource_group_id = var.resource_group_id
  amount            = var.monthly_budget_amount
  time_grain        = "Monthly"

  time_period {
    start_date = formatdate("YYYY-MM-01'T'00:00:00Z", timestamp())
  }

  notification {
    enabled        = true
    threshold      = 50
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Forecasted"
    contact_emails = var.alert_emails
    contact_groups = [azurerm_monitor_action_group.cost_guard.id]
  }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.alert_emails
    contact_groups = [azurerm_monitor_action_group.cost_guard.id]
  }

  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = var.alert_emails
    contact_groups = [azurerm_monitor_action_group.cost_guard.id]
  }

  lifecycle {
    ignore_changes = [time_period[0].start_date]
  }
}
