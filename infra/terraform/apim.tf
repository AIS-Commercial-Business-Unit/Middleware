##############################################################################
# Azure API Management
##############################################################################

# Public IP required for APIM management plane (stv2 platform) in VNet mode.
# Without this, port 3443 is unreachable and Terraform/ARM calls fail.
resource "azurerm_public_ip" "apim" {
  name                = "pip-apim-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  allocation_method   = "Static"
  sku                 = "Standard"
  domain_name_label   = "apim-${local.name_prefix}"

  tags = local.common_tags
}

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

  # Public IP for management plane access (required for stv2 platform)
  public_ip_address_id = azurerm_public_ip.apim.id

  virtual_network_configuration {
    subnet_id = azurerm_subnet.apim.id
  }

  depends_on = [azurerm_subnet_network_security_group_association.apim]

  tags = local.common_tags
}

# Grant APIM managed identity read access to Key Vault secrets
resource "azurerm_role_assignment" "apim_keyvault_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_api_management.main.identity[0].principal_id
}
