##############################################################################
# Azure API Management
##############################################################################

resource "azurerm_api_management" "main" {
  name                = "apim-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  publisher_name      = var.apim_publisher_name
  publisher_email     = var.apim_publisher_email
  sku_name            = var.apim_sku

  identity {
    type = "SystemAssigned"
  }

  # VNet integration (external mode) — APIM can resolve private DNS and reach ILB
  virtual_network_type = "External"

  virtual_network_configuration {
    subnet_id = azurerm_subnet.apim.id
  }

  tags = local.common_tags
}

# Grant APIM managed identity read access to Key Vault secrets
resource "azurerm_role_assignment" "apim_keyvault_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_api_management.main.identity[0].principal_id
}
