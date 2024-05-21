##########
# Variables
##########

# Constants
variable "measure_eval_label" {
  default = "MeasureEval"
}

## Service Variables
variable "measure_eval_db_connection_string" {
  description = "The MongoDB connection string to use for the MeasureEval service"
}
variable "measure_eval_db_name" {
  description = "The MongoDB database name to use for the MeasureEval service"
}

##########
# Key Vault Secrets
##########

resource "azurerm_key_vault_secret" "measure_eval_kv_mongo_connection_string" {
  name         = "measure-eval-mongo-connection-string"
  value        = var.measure_eval_db_connection_string
  key_vault_id = data.azurerm_key_vault.current.id
}

##########
# App Config Keys
##########

resource "azurerm_app_configuration_key" "measure_eval_ac_mongo_connection_string" {
  key                    = "/spring/data/mongodb/uri"
  type                   = "vault"
  label                  = var.measure_eval_label
  vault_key_reference    = azurerm_key_vault_secret.measure_eval_kv_mongo_connection_string.id
  configuration_store_id = data.azurerm_app_configuration.current.id
  
  depends_on = [
	azurerm_key_vault_secret.measure_eval_kv_mongo_connection_string
  ]
}

resource "azurerm_app_configuration_key" "measure_eval_ac_mongo_database" {
  key                    = "/spring/data/mongodb/database"
  type                   = "kv"
  value                  = var.measure_eval_db_name
  configuration_store_id = data.azurerm_app_configuration.current.id
  
  depends_on = [
	azurerm_key_vault_secret.measure_eval_kv_mongo_connection_string
  ]
}