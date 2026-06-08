##############################################################################
# Azure Kubernetes Service (AKS)
##############################################################################

resource "azurerm_kubernetes_cluster" "main" {
  name                = "aks-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "aks-${local.name_prefix}"
  kubernetes_version  = var.aks_kubernetes_version

  default_node_pool {
    name                = "default"
    node_count          = var.aks_node_count
    vm_size             = var.aks_node_vm_size
    os_disk_size_gb     = 50
    vnet_subnet_id      = azurerm_subnet.aks_nodes.id
    temporary_name_for_rotation = "temppool"
  }

  identity {
    type = "SystemAssigned"
  }

  # Workload Identity (required for SecretProviderClass + pod identity)
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # CSI Secrets Store Driver (for Key Vault SecretProviderClass)
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  # Web Application Routing addon (managed NGINX ingress controller)
  web_app_routing {
    dns_zone_ids = [azurerm_private_dns_zone.aks.id]
  }

  network_profile {
    network_plugin     = "azure"
    network_policy     = "azure"
    service_cidr       = "10.0.32.0/20"
    dns_service_ip     = "10.0.32.10"
    load_balancer_sku  = "standard"
  }

  oms_agent {
    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  }

  tags = local.common_tags
}

# Grant AKS kubelet identity access to ACR (if ACR is added later)
# For now, grant Key Vault access to the CSI driver identity
resource "azurerm_role_assignment" "aks_keyvault_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.main.key_vault_secrets_provider[0].secret_identity[0].object_id
}
