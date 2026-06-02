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

### 2026-06-01 — APIM per-host backend migration

- **Topology change:** Replaced the single shared `aks-internal` backend (host `api.middleware.internal`) with four per-API backends, one per hostname: `policy`, `file-processing`, `integration`, `appraisal` (all under `.middleware.internal`).
- **Two-layer fix required:** APIM's `set-backend-service backend-id="..."` in each API's `policy.xml` overrides the API's `serviceUrl`. So changing only `serviceUrl` would have been a silent no-op. Both layers must be updated together when migrating backends.
- **Pattern for per-host APIM backends in apiops:** one folder under `apim/backends/{name}/` per backend, `backendInformation.json` with `properties.url` set to `https://{host}`, and the matching `policy.xml` inside `apim/apis/{api}/` referencing `backend-id="{name}"`. The API's `serviceUrl` should be just the host (no path prefix); operation `urlTemplate` values stay unchanged because APIM concatenates them.
- **Coordination cost:** Path-prefix stripping at APIM only works if downstream ingress preserves the original path to the pod, OR if the app listens at short paths. When making this kind of change, confirm with the Platform/ingress owner.
- **apiops.yml note:** `backend:` block at the top of `apim/apiops.yml` is the apiops pipeline's global default, not a deployed APIM backend resource. Safe to leave alone when migrating per-API backends.
- **Files touched:** 4x `apim/apis/*/apiInformation.json` (serviceUrl), 4x `apim/apis/*/policy.xml` (backend-id), 4x new `apim/backends/*/backendInformation.json`, 1 deletion (`apim/backends/aks-internal/`).
