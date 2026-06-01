##############################################################################
# Azure SQL Server — NServiceBus Transport
##############################################################################

# Auto-generate SQL admin password and store in Key Vault
resource "random_password" "sql_admin" {
  length           = 24
  special          = true
  override_special = "!@#$%^&*"
}

resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.name_prefix}"
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = random_password.sql_admin.result
  minimum_tls_version          = "1.2"

  azuread_administrator {
    login_username = "middleware-workload"
    object_id      = azurerm_user_assigned_identity.workload.principal_id
  }

  tags = local.common_tags
}

resource "azurerm_mssql_database" "nsb" {
  name      = "middleware_nsb"
  server_id = azurerm_mssql_server.main.id
  sku_name  = "Basic"

  tags = local.common_tags
}

# Allow Azure services to access SQL Server
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
