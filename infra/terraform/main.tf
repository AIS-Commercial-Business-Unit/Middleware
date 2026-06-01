##############################################################################
# Terraform Configuration — Middleware Platform Azure Infrastructure
##############################################################################

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-middleware-tfstate"
    storage_account_name = "stmiddlewaretfstate"
    container_name       = "tfstate"
    key                  = "middleware.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
}

##############################################################################
# Resource Group
##############################################################################

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project}-${var.environment}"
  location = var.location

  tags = local.common_tags
}

##############################################################################
# Random suffix for globally-unique resource names
##############################################################################

resource "random_string" "suffix" {
  length  = 4
  special = false
  upper   = false
}

##############################################################################
# Locals
##############################################################################

locals {
  common_tags = {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }

  name_prefix    = "${var.project}-${var.environment}"
  unique_suffix  = random_string.suffix.result
}
