##############################################################################
# Outputs
##############################################################################

output "resource_group_name" {
  description = "Name of the resource group."
  value       = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  description = "Name of the AKS cluster."
  value       = azurerm_kubernetes_cluster.main.name
}

output "aks_cluster_fqdn" {
  description = "FQDN of the AKS cluster."
  value       = azurerm_kubernetes_cluster.main.fqdn
}

output "aks_oidc_issuer_url" {
  description = "OIDC issuer URL for workload identity federation."
  value       = azurerm_kubernetes_cluster.main.oidc_issuer_url
}

output "apim_gateway_url" {
  description = "APIM gateway URL."
  value       = azurerm_api_management.main.gateway_url
}

output "apim_name" {
  description = "APIM instance name."
  value       = azurerm_api_management.main.name
}

output "sql_server_fqdn" {
  description = "Azure SQL Server FQDN."
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "cosmos_connection_string" {
  description = "Cosmos DB (MongoDB) connection string."
  value       = azurerm_cosmosdb_account.main.primary_mongodb_connection_string
  sensitive   = true
}

output "eventhubs_namespace" {
  description = "Event Hubs namespace name."
  value       = azurerm_eventhub_namespace.main.name
}

output "eventhubs_kafka_endpoint" {
  description = "Event Hubs Kafka-compatible endpoint."
  value       = "${azurerm_eventhub_namespace.main.name}.servicebus.windows.net:9093"
}

output "storage_account_name" {
  description = "Blob Storage account name."
  value       = azurerm_storage_account.main.name
}

output "keyvault_uri" {
  description = "Key Vault URI."
  value       = azurerm_key_vault.main.vault_uri
}

output "keyvault_name" {
  description = "Key Vault name."
  value       = azurerm_key_vault.main.name
}

output "sql_server_name" {
  description = "Azure SQL Server name."
  value       = azurerm_mssql_server.main.name
}

output "cosmos_account_name" {
  description = "Cosmos DB account name."
  value       = azurerm_cosmosdb_account.main.name
}

output "appconfig_endpoint" {
  description = "App Configuration endpoint."
  value       = azurerm_app_configuration.main.endpoint
}

output "appinsights_connection_string" {
  description = "Application Insights connection string."
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID."
  value       = azurerm_log_analytics_workspace.main.id
}

output "workload_identity_client_id" {
  description = "Client ID for the workload managed identity. Use in K8s ServiceAccount annotation."
  value       = azurerm_user_assigned_identity.workload.client_id
}

output "workload_identity_tenant_id" {
  description = "Tenant ID for the workload managed identity."
  value       = azurerm_user_assigned_identity.workload.tenant_id
}

output "k8s_service_account_annotation" {
  description = "The annotation to add to your Kubernetes ServiceAccount."
  value       = "azure.workload.identity/client-id: ${azurerm_user_assigned_identity.workload.client_id}"
}

output "private_dns_zone_name" {
  description = "Private DNS zone for internal APIM → AKS routing."
  value       = azurerm_private_dns_zone.aks.name
}

output "ingress_internal_host" {
  description = "Internal hostname for API ingress (APIM backend)."
  value       = "api.${azurerm_private_dns_zone.aks.name}"
}

output "ingress_ilb_ip" {
  description = "Static internal IP for the ingress ILB."
  value       = local.ingress_ilb_ip
}

output "ilb_subnet_name" {
  description = "Subnet name where the ILB is deployed."
  value       = azurerm_subnet.aks_ilb.name
}

output "vnet_name" {
  description = "VNet name for the AKS cluster."
  value       = azurerm_virtual_network.main.name
}
