
##########
# Variables
##########

variable "local_uri_account" {}
variable "local_uri_audit" {}
variable "local_uri_census" {}
variable "local_uri_data_acquisition" {}
variable "local_uri_measure_eval" {}
variable "local_uri_normalization" {}
variable "local_uri_notification" {}
variable "local_uri_report" {}
variable "local_uri_submission" {}
variable "local_uri_tenant" {}
variable "local_uri_validation" {}

##########
# App Configs
##########

resource "azurerm_app_configuration_key" "global_ac_servicereg_account" {
  key                    = "ServiceRegistry:AccountServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_account
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_audit" {
  key                    = "ServiceRegistry:AuditServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_audit
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_census" {
  key                    = "ServiceRegistry:CensusServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_census
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_data_acquisition" {
  key                    = "ServiceRegistry:DataAcquisitionServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_data_acquisition
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_measure_eval" {
  key                    = "ServiceRegistry:MeasureServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_measure_eval
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_normalization" {
  key                    = "ServiceRegistry:NormalizationServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_normalization
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_notification" {
  key                    = "ServiceRegistry:NotificationServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_notification
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_report" {
  key                    = "ServiceRegistry:ReportServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_report
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_submission" {
  key                    = "ServiceRegistry:SubmissionServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_submission
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_tenant_check" {
  key                    = "ServiceRegistry:Tenant:CheckIfTenantExists"
  type                   = "kv"
  value					 = "true"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_tenant_get_endpoint" {
  key                    = "ServiceRegistry:Tenant:GetTenantRelativeEndpoint"
  type                   = "kv"
  value					 = "facility/"
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_tenant_uri" {
  key                    = "ServiceRegistry:Tenant:TenantServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_tenant
  configuration_store_id = data.azurerm_app_configuration.current.id
}

resource "azurerm_app_configuration_key" "global_ac_servicereg_validation_uri" {
  key                    = "ServiceRegistry:ValidationServiceUrl"
  type                   = "kv"
  value					 = var.local_uri_validation
  configuration_store_id = data.azurerm_app_configuration.current.id
}