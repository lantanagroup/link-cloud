# #Create a Container Apps Environment

# resource "azurerm_app_configuration" "aca-keith" {
#   name                = "aca-keith"
#   resource_group_name = var.RESOURCE_GROUP_NAME
#   location            = var.RESOURCE_GROUP_LOCATION
# }

# resource "azurerm_role_assignment" "appconf_dataowner" {
#   scope                = azurerm_app_configuration.aca-keith.id
#   role_definition_name = "App Configuration Data Owner"
#   principal_id         = data.azurerm_client_config.current.object_id
# }

# resource "azurerm_app_configuration_key" "KafkaConnection_GroupId" {
#   configuration_store_id = azurerm_app_configuration.aca-keith.id 
#   key                    = "KafkaConnection:GroupId"
#   label                  = "Census"
#   type                   = "kv"
#   value                  = "Census"

#   depends_on = [
#     azurerm_role_assignment.appconf_dataowner
#   ]
# }

# resource "azurerm_app_configuration_key" "test-keith3" {
#   configuration_store_id = azurerm_app_configuration.aca-keith.id
#   content_type           = "application/json"
#   key                    = "AllowReflection3"
#   label                  = "somelabel"
#   type                   = "kv"
#   value                  = "{\r\n    \"IncludeTestMessage\": true,\r\n    \"TestMessage\": \"[!** This is a TEST notification, please disregard **!]\",\r\n    \"SubjectTestMessage\": \"[!** TEST Notification - ACA **!]\",\r\n    \"Email\": true\r\n}"

#   depends_on = [
#     azurerm_role_assignment.appconf_dataowner
#   ]
# }

# resource "azurerm_app_configuration_key" "test-keith" {
#   configuration_store_id = azurerm_app_configuration.aca-keith.id
#   key                    = "AllowReflection2"
#   label                  = "somelabel"
#   type                   = "vault"
#   vault_key_reference    = azurerm_key_vault_secret.keith-secret.versionless_id


#   depends_on = [
#     azurerm_role_assignment.appconf_dataowner
#   ]
# }

# # resource "azurerm_app_configuration_key" "test-keith1" {
# #   configuration_store_id = azurerm_app_configuration.aca-keith.id
# #   key                    = "AllowReflection2"
# #   label                  = "somelabel"
# #   type                   = "kv"
# #   value                  = "false"

# #   depends_on = [
# #     azurerm_role_assignment.appconf_dataowner
# #   ]
# # }