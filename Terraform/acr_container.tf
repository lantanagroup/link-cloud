# # Create an Azure Container Registry within the resource group
# resource "azurerm_container_registry" "keith_containerRegistry" {
#   name                = "keith_containerRegistry"
#   resource_group_name = azurerm_resource_group.keith-rg.name
#   location            = azurerm_resource_group.keith-rg.location
#   sku                 = "Basic"
#   admin_enabled       = false

#   tags = {
#     environment = "dev"
#   }
# }