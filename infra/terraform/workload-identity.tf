##############################################################################
# Workload Identity — Managed Identity for Application Pods
#
# This creates a User-Assigned Managed Identity that app pods use to
# authenticate to Azure services (Cosmos DB, Event Hubs, Blob, SQL, etc.)
# via the AKS Workload Identity federation.
#
# In Kubernetes, pods reference this identity through a ServiceAccount
# annotated with: azure.workload.identity/client-id = <UAMI client ID>
##############################################################################

resource "azurerm_user_assigned_identity" "workload" {
  name                = "id-${local.name_prefix}-workload"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  tags = local.common_tags
}

# Federated credential: trust tokens from the AKS OIDC issuer for the
# "middleware" service account in the "middleware" namespace.
resource "azurerm_federated_identity_credential" "workload" {
  name                = "fed-${local.name_prefix}-workload"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.workload.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject             = "system:serviceaccount:${var.k8s_namespace}:${var.k8s_service_account}"
}

##############################################################################
# RBAC — Grant workload identity access to each Azure service
##############################################################################

# Cosmos DB (MongoDB) — data access via connection string stored in Key Vault
# MongoDB API does not support data-plane RBAC; connection string is pulled via SecretProviderClass.

# Event Hubs — send and receive messages (Kafka protocol)
resource "azurerm_role_assignment" "workload_eventhubs_sender" {
  scope                = azurerm_eventhub_namespace.main.id
  role_definition_name = "Azure Event Hubs Data Sender"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

resource "azurerm_role_assignment" "workload_eventhubs_receiver" {
  scope                = azurerm_eventhub_namespace.main.id
  role_definition_name = "Azure Event Hubs Data Receiver"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Blob Storage — read/write files
resource "azurerm_role_assignment" "workload_storage_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Key Vault — read secrets (for connection strings, etc.)
resource "azurerm_role_assignment" "workload_keyvault_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# App Configuration — read config values
resource "azurerm_role_assignment" "workload_appconfig_reader" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Azure SQL — AAD authentication (allows token-based login without password)
# The managed identity is added as an AAD admin on the SQL server.
# Apps use AccessToken auth in their connection string instead of user/password.

# Application Insights — write telemetry (Monitoring Metrics Publisher)
resource "azurerm_role_assignment" "workload_appinsights_publisher" {
  scope                = azurerm_application_insights.main.id
  role_definition_name = "Monitoring Metrics Publisher"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

##############################################################################
# Store Cosmos DB connection string in Key Vault
# (MongoDB API doesn't support AAD data-plane RBAC — use connection string)
##############################################################################

resource "azurerm_key_vault_secret" "cosmos_connection_string" {
  name         = "cosmos-mongodb-connection-string"
  value        = azurerm_cosmosdb_account.main.connection_strings[0]
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.deployer_keyvault_admin]
}
