# Create a resource group
resource "azurerm_resource_group" "keith-rg" {
  name     = "keith-rg"
  location = "South Central US"
  tags = {
    environment = "dev"
  }
}