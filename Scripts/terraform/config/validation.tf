##########
# Variables
##########

variable "validation_db_connection_string" {}
variable "validation_db_username" {}
variable "validation_db_password" {}

##########
# Key Vault Secrets
##########

resource "azurerm_key_vault_secret" "validation_kv_db_connection_string" {
  name         = "validation-db-connection-string"
  value        = var.validation_db_connection_string
  key_vault_id = data.azurerm_key_vault.current.id
}

resource "azurerm_key_vault_secret" "validation_kv_db_password" {
  name         = "validation-db-password"
  value        = var.validation_db_password
  key_vault_id = data.azurerm_key_vault.current.id
}

##########
# App Config Keys
##########

resource "azurerm_app_configuration_key" "validation_ac_db_connection_string" {
  key                    = "/spring/datasource/url"
  type                   = "vault"
  label                  = var.measure_eval_label
  vault_key_reference    = azurerm_key_vault_secret.measure_eval_kv_mongo_connection_string.id
  configuration_store_id = data.azurerm_app_configuration.current.id
  
  depends_on = [
	azurerm_key_vault_secret.measure_eval_kv_mongo_connection_string
  ]
}

resource "azurerm_app_configuration_key" "validation_ac_db_username" {
  key                    = "/spring/datasource/username"
  type                   = "kv"
  value                  = var.validation_db_username
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "validation_ac_db_password" {
  key                    = "/spring/datasource/password"
  type                   = "vault"
  label                  = var.measure_eval_label
  vault_key_reference    = azurerm_key_vault_secret.validation_kv_db_password.id
  configuration_store_id = data.azurerm_app_configuration.current.id
  
  depends_on = [
	azurerm_key_vault_secret.validation_kv_db_password
  ]
}