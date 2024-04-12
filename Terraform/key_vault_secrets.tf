# Create a Key Vault Secret within the resource group
resource "azurerm_key_vault_secret" "keith-secret" {
  name         = "KFC"
  value        = "11Herbs+Spices"
  key_vault_id = azurerm_key_vault.keith-LinkVault.id
}

