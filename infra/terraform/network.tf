##############################################################################
# Networking — VNet, Subnets, Static IP for Internal Load Balancer
##############################################################################

# VNet for the AKS cluster and internal services
resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = ["10.0.0.0/16"]

  tags = local.common_tags
}

# Subnet for AKS nodes (Azure CNI assigns pod IPs from this range)
resource "azurerm_subnet" "aks_nodes" {
  name                 = "snet-aks-nodes"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.0.0/20"] # ~4094 IPs for nodes + pods
}

# Subnet for internal load balancers (ingress ILB lives here)
resource "azurerm_subnet" "aks_ilb" {
  name                 = "snet-aks-ilb"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.16.0/24"] # /24 for LB frontend IPs
}

# Subnet for APIM (Developer tier VNet integration requires dedicated subnet)
resource "azurerm_subnet" "apim" {
  name                 = "snet-apim"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.17.0/24"]
}

# Static IP 10.0.16.10 is reserved from snet-aks-ilb for the ingress ILB.
# It's passed to the NginxIngressController via Helm values (loadBalancerAnnotations).
# Azure LB allocates it when the controller creates the internal Service.
# DNS A records all point to this IP; ingress-nginx routes by Host header:
#   - policy.middleware.internal           → policy-issuance-service
#   - file-processing.middleware.internal  → platform-file-processing-service
#   - integration.middleware.internal      → platform-integration-service
#   - appraisal.middleware.internal        → prs-appraisal-service
#   - ui.middleware.internal               → platform-ui
#   - api.middleware.internal              → (legacy, retained until APIM cutover)

# Grant AKS identity "Network Contributor" on the ILB subnet
# so AKS can deploy the internal load balancer with the static IP
resource "azurerm_role_assignment" "aks_ilb_subnet_contributor" {
  scope                = azurerm_subnet.aks_ilb.id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id
}

# Grant AKS identity "Network Contributor" on the node subnet
# (required for Azure CNI to assign pod IPs)
resource "azurerm_role_assignment" "aks_node_subnet_contributor" {
  scope                = azurerm_subnet.aks_nodes.id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id
}

# NSG for APIM subnet (required for APIM VNet integration)
resource "azurerm_network_security_group" "apim" {
  name                = "nsg-apim-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  # Allow APIM management traffic
  security_rule {
    name                       = "AllowAPIMManagement"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "3443"
    source_address_prefix      = "ApiManagement"
    destination_address_prefix = "VirtualNetwork"
  }

  # Allow Azure Load Balancer health probes
  security_rule {
    name                       = "AllowAzureLoadBalancer"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "6390"
    source_address_prefix      = "AzureLoadBalancer"
    destination_address_prefix = "VirtualNetwork"
  }

  # Allow inbound API traffic (port 443)
  security_rule {
    name                       = "AllowHTTPS"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "Internet"
    destination_address_prefix = "VirtualNetwork"
  }

  # Outbound: APIM → Azure Storage (config, logs, metrics)
  security_rule {
    name                       = "AllowStorageOutbound"
    priority                   = 100
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "Storage"
  }

  # Outbound: APIM → Azure SQL (internal config store)
  security_rule {
    name                       = "AllowSqlOutbound"
    priority                   = 110
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "1433"
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "Sql"
  }

  # Outbound: APIM → Azure AD (authentication)
  security_rule {
    name                       = "AllowAADOutbound"
    priority                   = 120
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "AzureActiveDirectory"
  }

  # Outbound: APIM → Azure Monitor (diagnostics, metrics)
  security_rule {
    name                       = "AllowAzureMonitorOutbound"
    priority                   = 130
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_ranges    = ["443", "1886"]
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "AzureMonitor"
  }

  tags = local.common_tags
}

resource "azurerm_subnet_network_security_group_association" "apim" {
  subnet_id                 = azurerm_subnet.apim.id
  network_security_group_id = azurerm_network_security_group.apim.id
}
