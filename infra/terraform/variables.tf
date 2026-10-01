variable "project_name" {
  type        = string
  default     = "quayside"
  description = "Project name"
}

variable "location" {
  type        = string
  default     = "francecentral"
  description = "Azure region"
}

variable "environment" {
  type        = string
  description = "Environment (e.g. dev, prod)"
}

variable "image_tag" {
  type        = string
  description = "Container image tag"
}

variable "ghcr_owner" {
  type        = string
  description = "GitHub Container Registry owner"
}

variable "ghcr_pat" {
  type        = string
  sensitive   = true
  description = "GitHub PAT with read:packages for ghcr.io authentication"
  default     = ""
}

variable "existing_container_app_environment_id" {
  type        = string
  default     = null
  description = "ID of an existing Container Apps Environment to reuse. Azure for Students allows one per subscription, so set this to the shared environment instead of creating a second."
}

variable "chat_model_version" {
  type        = string
  default     = "2025-08-07"
  description = "Version of the gpt-5-mini model deployment"
}

variable "embedding_model_version" {
  type        = string
  default     = "1"
  description = "Version of the text-embedding-3-small model deployment"
}

variable "openai_deployment_capacity" {
  type        = number
  default     = 100
  description = "Capacity (thousands of tokens per minute) for each GlobalStandard deployment"
}

variable "sql_admin_login" {
  type        = string
  sensitive   = true
  description = "SQL server administrator login"
}

variable "sql_admin_password" {
  type        = string
  sensitive   = true
  description = "SQL server administrator password"
}

variable "sql_aad_admin_login" {
  type        = string
  description = "Entra ID login name of the deploying principal, set as the SQL server's AAD administrator"
}

variable "sql_aad_admin_object_id" {
  type        = string
  description = "Entra ID object ID of the deploying principal"
}

variable "sql_aad_admin_tenant_id" {
  type        = string
  description = "Entra ID tenant ID"
}

variable "sql_owner_ip_address" {
  type        = string
  default     = ""
  description = "Owner's public IP, allowed through the SQL firewall for migrations and seeding. Empty adds no rule."
}

variable "sql_readonly_login" {
  type        = string
  default     = "quayside_reader"
  description = "Login granted db_datareader only, used for generated SQL. Created by the post-apply SQL script, not Terraform."
}

variable "sql_readonly_password" {
  type        = string
  sensitive   = true
  description = "Password of the read-only SQL login"
}

variable "ingest_args" {
  type        = list(string)
  default     = ["ingest"]
  description = "Container args that select the ingestion entrypoint of the shared image"
}

variable "ingest_schedule_cron" {
  type        = string
  default     = null
  description = "UTC cron expression for the ingestion job. Null makes the job manual-trigger only."
}

variable "alert_emails" {
  type        = list(string)
  description = "Emails that receive budget-threshold warnings. Required, there is no safe default for who gets billed."
}

variable "monthly_budget_amount" {
  type        = number
  default     = 20
  description = "Monthly spend cap in USD for this environment's resource group"
}

variable "cors_origins" {
  type        = list(string)
  description = "Origins the API allows via CORS. Must be real origins, not a wildcard."
}
