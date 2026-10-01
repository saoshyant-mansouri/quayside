resource "azurerm_user_assigned_identity" "api" {
  name                = "id-${var.prefix}-api"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_role_assignment" "openai_user" {
  scope                = var.openai_account_id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${var.prefix}-api"
  container_app_environment_id = var.container_app_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
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
    min_replicas = 1
    max_replicas = 3

    container {
      name   = "api"
      image  = "ghcr.io/${var.ghcr_owner}/quayside-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.api.client_id
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
      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      dynamic "env" {
        for_each = var.cors_origins
        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }
    }

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  depends_on = [azurerm_role_assignment.openai_user]

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }
}
