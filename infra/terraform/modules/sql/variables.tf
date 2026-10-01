variable "prefix" {
  type = string
}

variable "suffix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "administrator_login" {
  type      = string
  sensitive = true
}

variable "administrator_password" {
  type      = string
  sensitive = true
}

variable "aad_admin_login" {
  type = string
}

variable "aad_admin_object_id" {
  type = string
}

variable "aad_admin_tenant_id" {
  type = string
}

variable "owner_ip_address" {
  type    = string
  default = ""
}

variable "max_vcores" {
  type        = number
  default     = 4
  description = "Serverless maximum vCores. 4 is the ceiling that still supports the AutoPause free-limit behaviour; 6 and above only offer BillOverUsage."

  validation {
    condition     = contains([1, 2, 4], var.max_vcores)
    error_message = "Only 1, 2 or 4 vCores support freeLimitExhaustionBehavior = AutoPause. Higher SKUs bill once the free grant is exhausted."
  }
}
