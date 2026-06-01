##############################################################################
# Azure Blob Storage — File Processing
##############################################################################

resource "azurerm_storage_account" "main" {
  name                     = "st${replace(local.name_prefix, "-", "")}files"
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = local.common_tags
}

resource "azurerm_storage_container" "uploads" {
  name                  = "uploads"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "downloads" {
  name                  = "downloads"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}
