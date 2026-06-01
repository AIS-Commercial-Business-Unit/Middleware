##############################################################################
# Cosmos DB — MongoDB API (Serverless for dev)
##############################################################################

resource "azurerm_cosmosdb_account" "main" {
  name                = "cosmos-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  offer_type          = "Standard"
  kind                = "MongoDB"

  capabilities {
    name = "EnableMongo"
  }

  capabilities {
    name = "EnableServerless"
  }

  consistency_policy {
    consistency_level = var.cosmos_consistency_level
  }

  geo_location {
    location          = azurerm_resource_group.main.location
    failover_priority = 0
  }

  # MongoDB wire protocol version
  mongo_server_version = "4.2"

  tags = local.common_tags
}

# MongoDB databases for each bounded context
resource "azurerm_cosmosdb_mongo_database" "platform" {
  name                = "middleware-platform"
  resource_group_name = azurerm_resource_group.main.name
  account_name        = azurerm_cosmosdb_account.main.name
}
