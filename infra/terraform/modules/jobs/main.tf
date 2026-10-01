resource "azurerm_container_app_job" "ingest" {
  name                         = "caj-${var.prefix}-ingest"
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  replica_timeout_in_seconds   = 1800
  replica_retry_limit          = 0

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_id]
  }

  registry {
    server               = "ghcr.io"
    username             = var.ghcr_owner
    password_secret_name = "ghcr-pat"
  }

  secret {
    name  = "ghcr-pat"
    value = var.ghcr_pat
  }

  secret {
    name  = "sql-connection-string"
    value = var.sql_connection_string
  }

  secret {
    name  = "sql-readonly-connection-string"
    value = var.sql_readonly_connection_string
  }

  secret {
    name  = "app-insights-connection-string"
    value = var.app_insights_connection_string
  }

  template {
    container {
      name   = "ingest"
      image  = "ghcr.io/${var.ghcr_owner}/quayside-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"
      args   = var.args

      env {
        name  = "AZURE_CLIENT_ID"
        value = var.identity_client_id
      }
      env {
        name  = "AzureOpenAI__Endpoint"
        value = var.openai_endpoint
      }
      env {
        name  = "AzureOpenAI__ChatDeployment"
        value = var.chat_deployment_name
      }
      env {
        name  = "AzureOpenAI__EmbeddingDeployment"
        value = var.embedding_deployment_name
      }
      env {
        name        = "ConnectionStrings__Sql"
        secret_name = "sql-connection-string"
      }
      env {
        name        = "ConnectionStrings__SqlReadOnly"
        secret_name = "sql-readonly-connection-string"
      }
      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }
    }
  }

  dynamic "manual_trigger_config" {
    for_each = var.schedule_cron == null ? [1] : []
    content {
      parallelism              = 1
      replica_completion_count = 1
    }
  }

  dynamic "schedule_trigger_config" {
    for_each = var.schedule_cron == null ? [] : [1]
    content {
      cron_expression          = var.schedule_cron
      parallelism              = 1
      replica_completion_count = 1
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }
}
