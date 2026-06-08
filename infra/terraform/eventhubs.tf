##############################################################################
# Azure Event Hubs — Kafka-compatible Event Bus
##############################################################################

resource "azurerm_eventhub_namespace" "main" {
  name                = "evhns-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.eventhubs_sku
  capacity            = var.eventhubs_capacity

  # Kafka protocol is automatically available on Standard+ SKU

  tags = local.common_tags
}

# Authorization rule for application access (managed identity preferred at runtime)
resource "azurerm_eventhub_namespace_authorization_rule" "app" {
  name                = "app-access"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  listen              = true
  send                = true
  manage              = false
}

# Listen-only SAS policy for Kafdrop (read-only diagnostic UI)
resource "azurerm_eventhub_namespace_authorization_rule" "kafdrop_listen" {
  name                = "kafdrop-listen"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  listen              = true
  send                = false
  manage              = false
}

# Push connection string to Key Vault for CSI driver mount
resource "azurerm_key_vault_secret" "kafdrop_eh_conn" {
  name         = "eventhubs-kafdrop-connection-string"
  value        = azurerm_eventhub_namespace_authorization_rule.kafdrop_listen.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
}

##############################################################################
# Event Hub entities (Kafka topics) — one per domain event/command/DLQ
##############################################################################

locals {
  eventhub_topics = toset([
    # Policy Issuance
    "policy.commands.issue-policy",
    "policy.dlq.issuance-saga",
    "policy.dlq.renewal-batch",
    "policy.events.issuance-failed",
    "policy.events.issue-policy-requested",
    "policy.events.policy-issuance-initiated",
    "policy.events.policy-issued",
    "policy.events.renewal-record-failed",
    "policy.events.renewal-record-processed",

    # Compliance
    "compliance.dlq.compliance-check",
    "compliance.events.compliance-blocked",
    "compliance.events.compliance-cleared",

    # Customer Identity
    "customer.dlq.account-service",
    "customer.dlq.producer-lookup",
    "customer.events.account-lookup-requested",
    "customer.events.account-service-record-retrieved",
    "customer.events.customer-updated",

    # Platform Integration
    "integration.dlq.pas-gateway",
    "integration.events.policy-admin-system-call-failed",
    "integration.events.policy-admin-system-response-received",

    # Billing & Finance
    "billing.dlq.billing-association",
    "billing.events.billing-association-created",

    # Notification
    "notification.commands.publish-notification-intent",
    "notification.dlq.notification-dispatch",
    "notification.events.notification-dispatched",

    # File Processing
    "file.dlq.file-arrival",
    "file.dlq.record-outcome",
    "file.events.file-batch-completed",
    "file.events.file-batch-partial-failure",
    "file.events.file-batch-progress-updated",
    "file.events.file-batch-started",
    "file.events.renewal-record-ready-for-issuance",

    # PRS Appraisal
    "prs.dlq.riskid-gateway",
    "prs.events.appraisal-list-retrieved",
    "prs.events.appraisal-received",
    "prs.events.document-retrieved",
    "prs.events.producer-crossref-retrieved",
    "prs.events.producer-lookup-requested",
  ])
}

resource "azurerm_eventhub" "topics" {
  for_each = local.eventhub_topics

  name                = each.value
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  partition_count     = 2
  message_retention   = 1
}
