locals {
  prefix = "${var.project_name}-${var.environment}"

  sql_connection_string = join(";", [
    "Server=tcp:${module.sql.server_fqdn},1433",
    "Initial Catalog=${module.sql.database_name}",
    "User ID=${var.sql_admin_login}",
    "Password=${var.sql_admin_password}",
    "Encrypt=True",
    "TrustServerCertificate=False",
    "Connection Timeout=60",
  ])

  sql_readonly_connection_string = join(";", [
    "Server=tcp:${module.sql.server_fqdn},1433",
    "Initial Catalog=${module.sql.database_name}",
    "User ID=${var.sql_readonly_login}",
    "Password=${var.sql_readonly_password}",
    "Encrypt=True",
    "TrustServerCertificate=False",
    "Connection Timeout=60",
    "ApplicationIntent=ReadOnly",
  ])

  container_app_environment_id = coalesce(
    var.existing_container_app_environment_id,
    one(module.container_env[*].environment_id),
  )
}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

module "foundation" {
  source   = "./modules/foundation"
  prefix   = local.prefix
  location = var.location
}

module "openai" {
  source                  = "./modules/openai"
  prefix                  = local.prefix
  suffix                  = random_string.suffix.result
  location                = var.location
  resource_group_name     = module.foundation.resource_group_name
  chat_model_version      = var.chat_model_version
  embedding_model_version = var.embedding_model_version
  deployment_capacity     = var.openai_deployment_capacity
}

module "sql" {
  source                 = "./modules/sql"
  prefix                 = local.prefix
  suffix                 = random_string.suffix.result
  location               = var.location
  resource_group_name    = module.foundation.resource_group_name
  administrator_login    = var.sql_admin_login
  administrator_password = var.sql_admin_password
  aad_admin_login        = var.sql_aad_admin_login
  aad_admin_object_id    = var.sql_aad_admin_object_id
  aad_admin_tenant_id    = var.sql_aad_admin_tenant_id
  owner_ip_address       = var.sql_owner_ip_address
}

module "container_env" {
  count                      = var.existing_container_app_environment_id == null ? 1 : 0
  source                     = "./modules/container_env"
  prefix                     = local.prefix
  location                   = var.location
  resource_group_name        = module.foundation.resource_group_name
  log_analytics_workspace_id = module.foundation.log_analytics_workspace_id
}

module "api_app" {
  source                       = "./modules/api_app"
  prefix                       = local.prefix
  location                     = var.location
  resource_group_name          = module.foundation.resource_group_name
  container_app_environment_id = local.container_app_environment_id

  image_tag  = var.image_tag
  ghcr_owner = var.ghcr_owner
  ghcr_pat   = var.ghcr_pat

  openai_account_id         = module.openai.account_id
  openai_endpoint           = module.openai.endpoint
  chat_deployment_name      = module.openai.chat_deployment_name
  embedding_deployment_name = module.openai.embedding_deployment_name

  sql_connection_string          = local.sql_connection_string
  sql_readonly_connection_string = local.sql_readonly_connection_string
  app_insights_connection_string = module.foundation.app_insights_connection_string

  cors_origins = var.cors_origins
}

module "jobs" {
  source                       = "./modules/jobs"
  prefix                       = local.prefix
  location                     = var.location
  resource_group_name          = module.foundation.resource_group_name
  container_app_environment_id = local.container_app_environment_id

  image_tag  = var.image_tag
  ghcr_owner = var.ghcr_owner
  ghcr_pat   = var.ghcr_pat

  identity_id        = module.api_app.identity_id
  identity_client_id = module.api_app.identity_client_id

  openai_endpoint           = module.openai.endpoint
  chat_deployment_name      = module.openai.chat_deployment_name
  embedding_deployment_name = module.openai.embedding_deployment_name

  sql_connection_string          = local.sql_connection_string
  sql_readonly_connection_string = local.sql_readonly_connection_string
  app_insights_connection_string = module.foundation.app_insights_connection_string

  args          = var.ingest_args
  schedule_cron = var.ingest_schedule_cron
}

module "cost_guard" {
  source = "./modules/cost_guard"

  prefix                = local.prefix
  resource_group_id     = module.foundation.resource_group_id
  resource_group_name   = module.foundation.resource_group_name
  alert_emails          = var.alert_emails
  monthly_budget_amount = var.monthly_budget_amount

  depends_on = [module.sql, module.api_app]
}
