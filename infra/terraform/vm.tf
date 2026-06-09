##############################################################################
# Windows Jumpbox VM — Reaches AKS over the shared VNet
#
# Provisions a Standard_D2s_v5 Windows Server 2022 Datacenter (Azure Edition)
# VM in a dedicated subnet of the existing main VNet (10.0.0.0/16). Hosts the
# Particular Service Platform (ServiceControl, Monitoring, ServicePulse,
# RavenDB) installed via the Windows MSI. Auto-generates a strong admin
# password and stores both the username and password as secrets in the
# existing Key Vault.
##############################################################################

# ------------------------------------------------------------------------------
# Subnet — VM lives in its own /24 inside the main VNet
# ------------------------------------------------------------------------------
resource "azurerm_subnet" "vm" {
  name                 = "snet-vm"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.19.0/24"]
}

# ------------------------------------------------------------------------------
# NSG — SSH inbound from Internet, full outbound to VNet + Internet
# ------------------------------------------------------------------------------
resource "azurerm_network_security_group" "vm" {
  name                = "nsg-vm-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  # Inbound: RDP from Internet (matches dev parity with APIM allowing 443 from Internet)
  security_rule {
    name                       = "AllowRDPInbound"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "3389"
    source_address_prefix      = "Internet"
    destination_address_prefix = "VirtualNetwork"
  }

  # Inbound: HTTPS from Internet (ServicePulse UI / ServiceControl API)
  security_rule {
    name                       = "AllowHTTPSInbound"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "Internet"
    destination_address_prefix = "VirtualNetwork"
  }

  # Inbound: Particular Platform custom ports from Internet
  security_rule {
    name                       = "AllowParticularPlatformInbound"
    priority                   = 120
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_ranges    = ["33333", "33733"]
    source_address_prefix      = "Internet"
    destination_address_prefix = "VirtualNetwork"
  }

  # Outbound: VM → anything in the VNet (AKS pods, ILB, Key Vault PE, SQL, etc.)
  security_rule {
    name                       = "AllowVnetOutbound"
    priority                   = 100
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "VirtualNetwork"
  }

  # Outbound: VM → Internet (Windows Update, MSI downloads, Azure CLI, kubectl)
  security_rule {
    name                       = "AllowInternetOutbound"
    priority                   = 110
    direction                  = "Outbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_ranges    = ["80", "443"]
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "Internet"
  }

  tags = local.common_tags
}

resource "azurerm_subnet_network_security_group_association" "vm" {
  subnet_id                 = azurerm_subnet.vm.id
  network_security_group_id = azurerm_network_security_group.vm.id
}

# ------------------------------------------------------------------------------
# Public IP — Standard SKU, static, attached to the VM NIC
# ------------------------------------------------------------------------------
resource "azurerm_public_ip" "vm" {
  name                = "pip-vm-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  allocation_method   = "Static"
  sku                 = "Standard"

  tags = local.common_tags
}

# ------------------------------------------------------------------------------
# NIC
# ------------------------------------------------------------------------------
resource "azurerm_network_interface" "vm" {
  name                = "nic-vm-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  ip_configuration {
    name                          = "ipconfig1"
    subnet_id                     = azurerm_subnet.vm.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.vm.id
  }

  tags = local.common_tags
}

# ------------------------------------------------------------------------------
# Auto-generated admin password — meets Azure Windows VM complexity rules
# (12-123 chars, 3 of 4 categories: lower / upper / digit / special, must not
# contain the username or common reserved words)
# ------------------------------------------------------------------------------
resource "random_password" "vm_admin" {
  length           = 24
  special          = true
  override_special = "!@#$%^&*"
  min_lower        = 2
  min_upper        = 2
  min_numeric      = 2
  min_special      = 2
}

# ------------------------------------------------------------------------------
# Windows VM — Server 2022 Datacenter (Azure Edition)
# ------------------------------------------------------------------------------
resource "azurerm_windows_virtual_machine" "main" {
  name                = "vm-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  size                = var.vm_size
  admin_username      = var.vm_admin_username
  admin_password      = random_password.vm_admin.result
  computer_name       = "vm-particular"
  network_interface_ids = [azurerm_network_interface.vm.id]

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "Premium_LRS"
    disk_size_gb         = 128
  }

  source_image_reference {
    publisher = "MicrosoftWindowsServer"
    offer     = "WindowsServer"
    sku       = "2022-datacenter-azure-edition"
    version   = "latest"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# ------------------------------------------------------------------------------
# Key Vault secrets — username + auto-generated password
# ------------------------------------------------------------------------------
resource "azurerm_key_vault_secret" "vm_admin_username" {
  name         = "vm-admin-username"
  value        = var.vm_admin_username
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.deployer_keyvault_admin]
}

resource "azurerm_key_vault_secret" "vm_admin_password" {
  name         = "vm-admin-password"
  value        = random_password.vm_admin.result
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.deployer_keyvault_admin]
}
