provider "azurerm" {
  features {}
}

##########
# Variables
##########

variable "subscription_id" {}
variable "resource_group_name" {}
variable "key_vault_name" {}
variable "key_vault_uri" {}
variable "app_config_name" {}
variable "redis_connection" {
  description = "The host:port of the redis server"
}
variable "redis_password" {}
variable "auto_migrate" {
  type = bool
  description = "Whether or not to configure the applications to attempt to auto migrate the databases/schemas. Should ONLY be true in lower environments."
  default = false
}
variable "allow_reflection" {
  type = bool
  default = false
}
variable "enable_swagger" {
  type = bool
  default = false
}
variable "kafka_api_version_request" {
  type = bool
  default = false
}
variable "kafka_bootstrap_servers" {
  description = "JSON array of bootstrap server IPs or hostnames. I.E.: ['10.24.10.1:9092', 'some.bootstrap.server.local:9094']"
}
variable "otel_collector_endpoint" {}

##########
# Data
##########

data "azurerm_key_vault" "current" {
  name = var.key_vault_name
  resource_group_name = var.resource_group_name
}

data "azurerm_app_configuration" "current" {
  name = var.app_config_name
  resource_group_name = var.resource_group_name
}

##########
# Key Vault Secrets
##########

resource "azurerm_key_vault_secret" "global_kv_redis_password" {
  name         = "redis-password"
  value        = var.redis_password
  key_vault_id = data.azurerm_key_vault.current.id
}

##########
# App Configs
##########

resource "azurerm_app_configuration_key" "global_ac_java_ddl_auto" {
  key                    = "/spring/jpa/hibernate/ddl-auto"
  type                   = "kv"
  value                  = var.auto_migrate ? "update" : "none"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_enabled_swagger" {
  key                    = "EnableSwagger"
  type                   = "kv"
  value                  = var.enable_swagger ? "true" : "false"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_kafka_api_version_request" {
  key                    = "KafkaConnection:ApiVersionRequest"
  type                   = "kv"
  value                  = var.kafka_api_version_request ? "true" : "false"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_kafka_bootstrap_server" {
  key                    = "KafkaConnection:BootstrapServers"
  type                   = "kv"
  value                  = var.kafka_bootstrap_servers
  content_type           = "application/json"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_cors" {
  key                    = "CORS"
  type                   = "kv"
  value                  = "{\"AllowAllHeaders\":true,\"AllowAllMethods\":true,\"AllowAllOrigins\":true,\"AllowCredentials\":true,\"AllowedExposedHeaders\":[\"X-Pagination\"],\"MaxAge\":600}"
  content_type           = "application/json"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

# This is not a KV because it's not actually a full connection string, it's simply the host:port
resource "azurerm_app_configuration_key" "global_ac_redis_connection" {
  key                    = "ConnectionStrings:Redis"
  type                   = "kv"
  value					 = var.redis_connection
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_redis_password" {
  key                    = "Redis:Password"
  type                   = "vault"
  vault_key_reference    = azurerm_key_vault_secret.global_kv_redis_password.id
  configuration_store_id = data.azurerm_app_configuration.current.id
  
  depends_on = [
	azurerm_key_vault_secret.validation_kv_db_password
  ]
}

resource "azurerm_app_configuration_key" "global_ac_secret_mgmt_enabled" {
  key                    = "SecretManagement:Enabled"
  type                   = "kv"
  value					 = "true"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_secret_mgmt_manager" {
  key                    = "SecretManagement:Manager"
  type                   = "kv"
  value					 = "AzureKeyVault"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_secret_mgmt_manager_uri" {
  key                    = "SecretManagement:ManagerUri"
  type                   = "kv"
  value					 = "https://nhsn-dev-kv-link-cl.vault.azure.net/"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_telementry_otel_enabled" {
  key                    = "Telemetry:EnableOtelCollector"
  type                   = "kv"
  value					 = "true"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_telementry_otel_endpoint" {
  key                    = "Telemetry:OtelCollectorEndpoint"
  type                   = "kv"
  value					 = "http://collector"
  configuration_store_id = data.azurerm_app_configuration.current.id
}