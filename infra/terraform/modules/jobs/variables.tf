variable "prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "container_app_environment_id" {
  type = string
}

variable "image_tag" {
  type = string
}

variable "ghcr_owner" {
  type = string
}

variable "ghcr_pat" {
  type      = string
  sensitive = true
  default   = ""
}

variable "identity_id" {
  type = string
}

variable "identity_client_id" {
  type = string
}

variable "openai_endpoint" {
  type = string
}

variable "chat_deployment_name" {
  type = string
}

variable "embedding_deployment_name" {
  type = string
}

variable "sql_connection_string" {
  type      = string
  sensitive = true
}

variable "sql_readonly_connection_string" {
  type      = string
  sensitive = true
}

variable "app_insights_connection_string" {
  type      = string
  sensitive = true
}

variable "args" {
  type    = list(string)
  default = ["ingest"]
}

variable "schedule_cron" {
  type    = string
  default = null
}
