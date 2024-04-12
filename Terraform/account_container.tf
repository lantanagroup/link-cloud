data "azurerm_container_registry" "acr" {
  name                = "lantanagroup"
  resource_group_name = "internal"
}

resource "azurerm_container_app" "keith-container" {
  name                         = "keith-container"
  container_app_environment_id = azurerm_container_app_environment.keith-apps.id
  resource_group_name          = var.RESOURCE_GROUP_NAME
  revision_mode                = "Single"

  identity {
    type = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.containerapp.id ]
  }

  registry {
    server = data.azurerm_container_registry.acr.login_server
    identity = azurerm_user_assigned_identity.containerapp.id
  }

  template {
    container {
      name   = "keith-container2"
#      image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
      image = "lantanagroup.azurecr.io/link-account:latest"
      cpu    = 0.25
      memory = "0.5Gi"
      env {
        name = "Link__Audit__ExternalConfigurationSource"
        value = "AzureAppConfiguration"
      }
      env {
        name = "ConnectionStrings__AzureAppConfiguration"
        secret_name = "azure-appconfiguration-endpoint"
        value = "azure-appconfiguration-endpoint"
      }
    }
  }
  secret {
    name = "azure-appconfiguration-endpoint"
    value = "https://aca-configs.azconfig.io"
  }
  secret {
    name = "acr-pass"
    value = "kcf5al3jhYsohrBgbf/9Y4G4MDwdxDxz"
  }
}

resource "azurerm_user_assigned_identity" "containerapp" {
  location            = var.RESOURCE_GROUP_LOCATION
  name                = "containerappmi"
  resource_group_name = var.RESOURCE_GROUP_NAME
}

resource "azurerm_role_assignment" "containerapp" {
  scope                = data.azurerm_container_registry.acr.id
  role_definition_name = "acrpull"
  principal_id         = azurerm_user_assigned_identity.containerapp.principal_id
  depends_on = [
    azurerm_user_assigned_identity.containerapp
  ]
}


# data "azurerm_container_registry" "lantanagroup" {
#   name                = "nhsnlink"
#   resource_group_name = "internal"

# }
# output "login_server" {
#   value = data.azurerm_container_registry.lantanagroup.login_server
# }
# output "admin_username" {
#   value = data.azurerm_container_registry.lantanagroup.admin_username
# }

# resource "azurerm_container_app" "keith2" {
#   name                         = "keith2"
#   container_app_environment_id = azurerm_container_app_environment.keith-apps.id
#   resource_group_name          = var.RESOURCE_GROUP_NAME
#   revision_mode                = "Single"
#   template {
#     container {
#       name   = "examplecontainerapp"
#       image  = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
#       cpu    = 0.25
#       memory = "0.5Gi"
#     }
#   }
# }

