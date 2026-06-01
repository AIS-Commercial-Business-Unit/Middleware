##############################################################################
# Input Variables
##############################################################################

variable "project" {
  description = "Project name used in resource naming and tagging."
  type        = string
  default     = "middleware"
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "eastus2"
}

# ------------------------------------------------------------------------------
# AKS
# ------------------------------------------------------------------------------

variable "aks_node_count" {
  description = "Number of nodes in the default AKS node pool."
  type        = number
  default     = 2
}

variable "k8s_namespace" {
  description = "Kubernetes namespace where middleware pods run."
  type        = string
  default     = "middleware"
}

variable "k8s_service_account" {
  description = "Kubernetes service account name annotated with workload identity."
  type        = string
  default     = "middleware-workload"
}

variable "aks_node_vm_size" {
  description = "VM size for AKS default node pool."
  type        = string
  default     = "Standard_B2s"
}

variable "aks_kubernetes_version" {
  description = "Kubernetes version for AKS."
  type        = string
  default     = "1.29"
}

# ------------------------------------------------------------------------------
# APIM
# ------------------------------------------------------------------------------

variable "apim_sku" {
  description = "APIM SKU. Developer_1 required for VNet integration to reach internal backends."
  type        = string
  default     = "Developer_1"
}

variable "apim_publisher_name" {
  description = "Publisher name for APIM."
  type        = string
  default     = "Middleware Platform"
}

variable "apim_publisher_email" {
  description = "Publisher email for APIM."
  type        = string
}

# ------------------------------------------------------------------------------
# Azure SQL
# ------------------------------------------------------------------------------

variable "sql_admin_login" {
  description = "SQL Server administrator login name."
  type        = string
  default     = "sqladmin"
}

# ------------------------------------------------------------------------------
# Cosmos DB
# ------------------------------------------------------------------------------

variable "cosmos_consistency_level" {
  description = "Cosmos DB consistency level."
  type        = string
  default     = "Session"
}

# ------------------------------------------------------------------------------
# Event Hubs
# ------------------------------------------------------------------------------

variable "eventhubs_sku" {
  description = "Event Hubs namespace SKU (Basic, Standard)."
  type        = string
  default     = "Standard"
}

variable "eventhubs_capacity" {
  description = "Event Hubs namespace throughput units."
  type        = number
  default     = 1
}

# ------------------------------------------------------------------------------
# Key Vault
# ------------------------------------------------------------------------------

variable "keyvault_sku" {
  description = "Key Vault SKU (standard, premium)."
  type        = string
  default     = "standard"
}
