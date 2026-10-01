output "resource_group_name" {
  value = module.foundation.resource_group_name
}

output "api_fqdn" {
  value = module.api_app.fqdn
}

output "api_app_name" {
  value = module.api_app.name
}

output "openai_endpoint" {
  value = module.openai.endpoint
}

output "sql_server_fqdn" {
  value = module.sql.server_fqdn
}

output "sql_database_name" {
  value = module.sql.database_name
}

output "ingest_job_name" {
  value = module.jobs.job_name
}

output "budget_name" {
  value = module.cost_guard.budget_name
}
