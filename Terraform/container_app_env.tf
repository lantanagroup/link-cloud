#Create a Container Apps Environment
resource "azurerm_container_app_environment" "keith-apps" {
  name                       = "keith-apps"
  location                   = azurerm_resource_group.keith-rg.location
  resource_group_name        = var.RESOURCE_GROUP_NAME
  log_analytics_workspace_id = azurerm_log_analytics_workspace.link-log-analytics.id
}
