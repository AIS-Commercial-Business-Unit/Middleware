# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Azure APIM, App Insights, Azure Monitor, Key Vault, App Configuration, Blob Storage, Azure SignalR Service, Entra ID Managed Identities, AKS
- **Key principle:** Managed Identity for all service-to-service auth; APIM as front door for all APIs; Key Vault references replace all literal secrets
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-06-01 — Kafdrop → Event Hubs auth recipe

- **Chose Option B (SAS + SASL_PLAIN)** over Workload Identity + OAUTHBEARER for Kafdrop. Kafdrop's fat-jar image has no Azure SDK on its classpath and Spring Boot's `JarLauncher` ignores `-Dloader.path`, so OAUTHBEARER would require a forked image. Not worth the toil for a read-only diagnostic UI.
- **Pre-flight blocker flagged:** Event Hubs Kafka is on TCP/9093, not 443. The user said 9093 is firewalled outbound. Same constraint applies to the existing Java services — flagged for Platform/Network to confirm egress before Kafdrop ships.
- **Pattern for non-Entra-aware images talking to Event Hubs:** namespace-scoped SAS policy with Listen-only claim → Key Vault secret → CSI driver file mount → render kafka.properties at pod start with a `command:` shim (or init container) → base64 into `KAFKA_PROPERTIES` env var. Avoids baking the secret into the chart and avoids env-var leakage in `kubectl describe pod`.
- **Kafdrop accepts both `KAFKA_PROPERTIES` (base64) and `KAFKA_PROPERTIES_FILE` (path)** — file mode is friendlier when the properties content includes a secret.
- **Recipe deliverable:** `.squad/decisions/inbox/azure-kafdrop-eventhubs.md` with env vars, both Option A and Option B documented, Helm values block, Terraform additions, and the 9093 egress callout.

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

## 2026-06-01: Dual-stack APIM onboarding for .NET HTTP services
- Added apim/apis/dotnet-policy-issuance-api and apim/apis/dotnet-file-processing-api mirroring Java siblings; specification.yaml copied verbatim (routes identical between stacks).
- Added apim/backends/dotnet-policy-issuance and apim/backends/dotnet-file-processing pointing at dotnet-policy.middleware.internal and dotnet-file-processing.middleware.internal.
- Naming: dotnet- PREFIX uniformly across folder, APIM path, backend id, ingress host. displayName suffixed with (.NET). Java stack untouched.
- policy.xml uses <set-backend-service backend-id="dotnet-{name}" /> per decision #50.
- 7 event-only .NET services skipped — no HTTP surface to onboard.
- Helm/Terraform/DNS deliberately not touched — Platform owns ingress + A records for the new hosts.
- Validation: each new hostname appears in exactly its API serviceUrl + its backend url; all 6 APIM path values unique (policy-issuance, dotnet-policy-issuance, file-processing, dotnet-file-processing, platform-integration, prs-appraisal).

### 2026-06-01 — .NET APIM onboarding revalidated

- Re-ran the onboarding task; all artifacts already in place from the prior session run. No edits needed.
- Verified: 'apim/apis/dotnet-policy-issuance-api/' and 'apim/apis/dotnet-file-processing-api/' each contain apiInformation.json + policy.xml + specification.yaml mirroring their Java twins. specification.yaml is identical between stacks (routes match — controllers own 'api/v1' / 'api/v1/policies').
- Verified: 'apim/backends/dotnet-policy-issuance/' and 'apim/backends/dotnet-file-processing/' each contain backendInformation.json with url 'https://dotnet-policy.middleware.internal' and 'https://dotnet-file-processing.middleware.internal' respectively.
- Verified: each .NET policy.xml '<set-backend-service backend-id="dotnet-{name}" />' references the correct per-stack backend.
- Verified: APIM 'path' values are unique across stacks ('policy-issuance' / 'dotnet-policy-issuance', 'file-processing' / 'dotnet-file-processing'). No client URL collision.
- Tag/group structure: confirmed the apiops layout has no 'apim/tags/' folder and Java APIs do not carry a tags.json — there is no existing convention to mirror, so per task constraint ('don't invent structure that isn't already there') no tags were added. If a stack-discrimination tag is wanted later, it'd be a layout-wide change introduced uniformly across Java + .NET APIs.
- Decision drop file '.squad/decisions/inbox/azure-dotnet-apim-onboard.md' is already present from the prior run; no duplicate created.
