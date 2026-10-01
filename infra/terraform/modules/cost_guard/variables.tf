variable "prefix" {
  type        = string
  description = "Naming prefix, e.g. quayside-dev"
}

variable "resource_group_id" {
  type        = string
  description = "ID of the resource group the budget scopes to"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the resource group the action group lives in"
}

variable "alert_emails" {
  type        = list(string)
  description = "Where budget-threshold notifications are sent. Required, there is no safe default."
}

variable "monthly_budget_amount" {
  type        = number
  default     = 20
  description = "Monthly spend cap in USD for this resource group"
}
