# We strongly recommend using the required_providers block to set the
# Azure Provider source and version being used
terraform {
  required_providers { 
    azurerm = {
      source = "hashicorp/azurerm"
      version = "=3.97.1"
    }
  }
}
provider "azurerm" {
  features {}

  # subscription_id   = "e908bdf1-23ff-4c4c-949a-7a13a296e36d"
  # tenant_id         = "5934dd1c-8d18-4de5-8696-4516a5707a57"
  # client_id         = "ad9d8b2e-a9a9-42b9-929f-8103e0b8d8c1"
  # client_secret     = "xu18Q~D.Z0WduOP8PcdKMXCW5RIaRi9F4Qa~9bZl"
}