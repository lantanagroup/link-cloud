# Create a Log Analytics Workspace within the resource group

resource "azurerm_log_analytics_workspace" "link-log-analytics" {
  name                = "link-log-analytics"
  location            = azurerm_resource_group.keith-rg.location
  resource_group_name = var.RESOURCE_GROUP_NAME
  sku                 = "PerGB2018"
  retention_in_days   = 30
}
