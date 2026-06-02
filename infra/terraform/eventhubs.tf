##############################################################################
# Azure Event Hubs — Kafka-compatible Event Bus
##############################################################################

resource "azurerm_eventhub_namespace" "main" {
  name                = "evhns-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.eventhubs_sku
  capacity            = var.eventhubs_capacity

  # Kafka protocol is automatically available on Standard+ SKU

  tags = local.common_tags
}

# Authorization rule for application access (managed identity preferred at runtime)
resource "azurerm_eventhub_namespace_authorization_rule" "app" {
  name                = "app-access"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  listen              = true
  send                = true
  manage              = false
}

# Listen-only SAS policy for Kafdrop (read-only diagnostic UI)
resource "azurerm_eventhub_namespace_authorization_rule" "kafdrop_listen" {
  name                = "kafdrop-listen"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  listen              = true
  send                = false
  manage              = false
}

# Push connection string to Key Vault for CSI driver mount
resource "azurerm_key_vault_secret" "kafdrop_eh_conn" {
  name         = "eventhubs-kafdrop-connection-string"
  value        = azurerm_eventhub_namespace_authorization_rule.kafdrop_listen.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
}
