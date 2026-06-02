##############################################################################
# DNS — Private DNS Zone for APIM → AKS internal routing
##############################################################################

locals {
  # Static IP reserved from snet-aks-ilb (10.0.16.0/24) for ingress ILB
  ingress_ilb_ip = "10.0.16.10"
}

# Private DNS Zone for internal service routing (APIM → AKS ingress)
resource "azurerm_private_dns_zone" "aks" {
  name                = "middleware.internal"
  resource_group_name = azurerm_resource_group.main.name

  tags = local.common_tags
}

# Link the private DNS zone to the VNet so APIM and other services can resolve it
resource "azurerm_private_dns_zone_virtual_network_link" "aks" {
  name                  = "aks-dns-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.aks.name
  virtual_network_id    = azurerm_virtual_network.main.id
  registration_enabled  = false

  tags = local.common_tags
}

# A record: api.middleware.internal → ILB static IP
# TODO: Retained for backward compatibility. Backend APIs now use per-service
# hostnames (policy, file-processing, integration, appraisal). This record can
# be removed once APIM backend definitions are updated to the new hostnames.
resource "azurerm_private_dns_a_record" "api" {
  name                = "api"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

# Per-service A records — one hostname per backend API. All point to the same
# ingress ILB; ingress-nginx routes by Host header to the matching Service.
resource "azurerm_private_dns_a_record" "policy" {
  name                = "policy"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

resource "azurerm_private_dns_a_record" "file_processing" {
  name                = "file-processing"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

resource "azurerm_private_dns_a_record" "integration" {
  name                = "integration"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

resource "azurerm_private_dns_a_record" "appraisal" {
  name                = "appraisal"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

# Observability — Kafdrop (Kafka UI) and Grafana ingress hostnames. Both
# resolve to the same ILB; ingress-nginx routes by Host header.
resource "azurerm_private_dns_a_record" "kafdrop" {
  name                = "kafdrop"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

resource "azurerm_private_dns_a_record" "grafana" {
  name                = "grafana"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

# A record: ui.middleware.internal → ILB static IP (same ILB, different host header)
resource "azurerm_private_dns_a_record" "ui" {
  name                = "ui"
  zone_name           = azurerm_private_dns_zone.aks.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [local.ingress_ilb_ip]
}

# Grant Web App Routing addon identity access to the Private DNS Zone
resource "azurerm_role_assignment" "web_app_routing_dns" {
  scope                = azurerm_private_dns_zone.aks.id
  role_definition_name = "Private DNS Zone Contributor"
  principal_id         = azurerm_kubernetes_cluster.main.web_app_routing[0].web_app_routing_identity[0].object_id
}

# Grant Web App Routing addon identity access to Key Vault (for future cert use)
resource "azurerm_role_assignment" "web_app_routing_keyvault" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.main.web_app_routing[0].web_app_routing_identity[0].object_id
}
