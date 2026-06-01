##############################################################################
# Application Gateway — Public entry point for platform UI
##############################################################################

# Dedicated subnet for App Gateway (required, no other resources allowed)
resource "azurerm_subnet" "appgw" {
  name                 = "snet-appgw"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.18.0/24"]
}

# Public IP with DNS label
resource "azurerm_public_ip" "appgw" {
  name                = "pip-appgw-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  allocation_method   = "Static"
  sku                 = "Standard"
  domain_name_label   = "${var.project}-${var.environment}"

  tags = local.common_tags
}

# Application Gateway v2 (Standard for dev — upgrade to WAF_v2 for prod)
resource "azurerm_application_gateway" "main" {
  name                = "appgw-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  sku {
    name     = "Standard_v2"
    tier     = "Standard_v2"
  }

  autoscale_configuration {
    min_capacity = 0
    max_capacity = 2
  }

  ssl_policy {
    policy_type = "Predefined"
    policy_name = "AppGwSslPolicy20220101"
  }

  gateway_ip_configuration {
    name      = "gateway-ip-config"
    subnet_id = azurerm_subnet.appgw.id
  }

  # Frontend: public IP
  frontend_ip_configuration {
    name                 = "frontend-ip"
    public_ip_address_id = azurerm_public_ip.appgw.id
  }

  frontend_port {
    name = "http-port"
    port = 80
  }

  # Backend pool: AKS ILB at 10.0.16.10
  backend_address_pool {
    name  = "aks-ilb-backend"
    ip_addresses = ["10.0.16.10"]
  }

  # Backend HTTP settings: route to UI ingress
  backend_http_settings {
    name                  = "ui-http-settings"
    cookie_based_affinity = "Disabled"
    port                  = 80
    protocol              = "Http"
    request_timeout       = 30
    host_name             = "ui.middleware.internal"

    probe_name = "ui-health-probe"
  }

  # Health probe for the UI
  probe {
    name                = "ui-health-probe"
    host                = "ui.middleware.internal"
    path                = "/"
    interval            = 30
    timeout             = 10
    unhealthy_threshold = 3
    protocol            = "Http"

    match {
      status_code = ["200-399"]
    }
  }

  # HTTP listener
  http_listener {
    name                           = "http-listener"
    frontend_ip_configuration_name = "frontend-ip"
    frontend_port_name             = "http-port"
    protocol                       = "Http"
  }

  # Routing rule: HTTP → AKS ILB backend
  request_routing_rule {
    name                       = "ui-routing-rule"
    priority                   = 100
    rule_type                  = "Basic"
    http_listener_name         = "http-listener"
    backend_address_pool_name  = "aks-ilb-backend"
    backend_http_settings_name = "ui-http-settings"
  }

  tags = local.common_tags
}
