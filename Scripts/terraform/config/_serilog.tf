##########
# Variables
##########

##########
# App Configs
##########

resource "azurerm_app_configuration_key" "global_ac_serilog" {
  key                    = "Serilog"
  type                   = "kv"
  value					 = "{\"MinimumLevel\":{\"Default\":\"Information\",\"Override\":{\"Microsoft\":\"Warning\",\"System\":\"Warning\"}},\"Using\":[\"Serilog.Sinks.Console\",\"Serilog.Sinks.Grafana.Loki\"],\"WriteTo\":[{\"Name\":\"Console\"},{\"Args\":{\"labels\":[{\"key\":\"app\",\"value\":\"Link DEV\"},{\"key\":\"component\",\"value\":\"\"}],\"propertiesAsLabels\":[\"app\",\"component\"],\"uri\":\"\"},\"Name\":\"GrafanaLoki\"}]}"
  content_type           = "application/json"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_serilog_uri" {
  key                    = "Serilog:WriteTo:1:Args:uri"
  type                   = "kv"
  value					 = var.key_vault_uri
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_serilog_component_account" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Account"
  label                  = "Account"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_audit" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Audit"
  label                  = "Audit"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_census" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Census"
  label                  = "Census"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_data_acquisition" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "DataAcquisition"
  label                  = "DataAcquisition"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_link_admin_bff" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "LinkAdminBFF"
  label                  = "LinkAdminBFF"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_normalization" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Normalization"
  label                  = "Normalization"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_notification" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Notification"
  label                  = "Notification"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_query_dispatch" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "QueryDispatch"
  label                  = "QueryDispatch"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_report" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Report"
  label                  = "Report"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_submission" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Submission"
  label                  = "Submission"
  configuration_store_id = data.azurerm_app_configuration.current.id
}
 
resource "azurerm_app_configuration_key" "global_ac_serilog_component_tenant" {
  key                    = "Serilog:WriteTo:1:Args:labels:1:value"
  type                   = "kv"
  value                  = "Tenant"
  label                  = "Tenant"
  configuration_store_id = data.azurerm_app_configuration.current.id
}