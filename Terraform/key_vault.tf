# Create a Key Vault within the resource group
data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "keith-LinkVault" {
  name                        = "keith-LinkVault"
  resource_group_name         = var.RESOURCE_GROUP_NAME
  location                    = var.RESOURCE_GROUP_LOCATION
  enabled_for_disk_encryption = true
  soft_delete_retention_days  = 7
  sku_name                    = "standard"
  tenant_id                   = data.azurerm_client_config.current.tenant_id

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    key_permissions = [
      "Get","List","Create","Delete","Purge"
    ]

    secret_permissions = [
      "Get","List","Set","Delete","Purge"
    ]

    storage_permissions = [
      "Get","List","Set","Delete","Purge"
    ]
  }

  tags = {
    environment = "dev"
  }
}