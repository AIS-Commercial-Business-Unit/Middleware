# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Azure APIM, App Insights, Azure Monitor, Key Vault, App Configuration, Blob Storage, Azure SignalR Service, Entra ID Managed Identities, AKS
- **Key principle:** Managed Identity for all service-to-service auth; APIM as front door for all APIs; Key Vault references replace all literal secrets
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-06-01 — Terraform Infrastructure Scaffold

- **Decision:** All Azure infra managed via Terraform in `infra/terraform/` with modular file-per-concern layout.
- **Auth pattern:** GitHub Actions uses OIDC (federated credentials) — no service principal secrets stored. ARM_USE_OIDC=true for azurerm provider.
- **Key Vault access:** Applications use SecretProviderClass (CSI driver) on AKS, NOT SDK calls. AKS has `key_vault_secrets_provider` enabled with `secret_rotation_enabled = true`.
- **Workload Identity:** AKS has `oidc_issuer_enabled` + `workload_identity_enabled` for pod-level Managed Identity federation.
- **Event Hubs:** Using Standard SKU with `kafka_enabled = true` — required for Kafka protocol compatibility.
- **Cosmos DB:** MongoDB API with serverless capability for dev (cheapest option that still supports MongoDB wire protocol).
- **APIM:** Consumption tier for dev (pay-per-call, cheapest). SystemAssigned identity for Key Vault integration.
- **Remote state:** Azure Storage backend (`rg-middleware-tfstate` / `stmiddlewaretfstate` / `tfstate` container).
- **Workflow pattern:** Plan → artifact upload → approval gate (GitHub Environment) → Apply. Separate `-plan` environments allow auto-running plan without approval.
- **Key file paths:** `infra/terraform/*.tf`, `.github/workflows/deploy-infra.yml`, `infra/README.md`
- **User preference:** Step-by-step README for developers new to Azure IaC, with actual `az` CLI commands for Windows (PowerShell).
