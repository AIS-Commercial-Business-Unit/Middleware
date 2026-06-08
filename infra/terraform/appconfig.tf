##############################################################################
# Azure App Configuration — Centralized Configuration
##############################################################################

resource "azurerm_app_configuration" "main" {
  name                = "appcs-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "free"

  tags = local.common_tags
}

# Grant AKS managed identity read access to App Configuration
resource "azurerm_role_assignment" "aks_appconfig_reader" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id
}
