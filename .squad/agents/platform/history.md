# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** AKS, Kubernetes manifests, Helm, Terraform/Bicep, KEDA (Kafka-triggered autoscaling), Key Vault CSI driver
- **Key principle:** All K8s resources in source control; KEDA scales consumers based on Kafka lag; namespace per domain service
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-06-01 — Per-service hostnames replace path-based ingress
- Restructured AKS ingress: each backend API now has its own hostname instead of sharing `api.middleware.internal` with path-based routing
- New hostnames (all point to ILB 10.0.16.10, ingress-nginx routes by Host header):
  - `policy.middleware.internal` → policy-issuance-service
  - `file-processing.middleware.internal` → platform-file-processing-service
  - `integration.middleware.internal` → platform-integration-service
  - `appraisal.middleware.internal` → prs-appraisal-service
- Ingress paths reduced to `/` (Prefix) per host — Java controllers keep their full `@RequestMapping` paths; ingress no longer rewrites or filters
- `api.middleware.internal` retained in DNS + global values comment until APIM backend definitions are cut over (TODO marker added)
- Terraform: 4 new `azurerm_private_dns_a_record` resources in `dns.tf` (`policy`, `file_processing`, `integration`, `appraisal`)
- `network.tf` comment block updated to enumerate all hostnames
- **Validation:** `helm dependency build` then `helm template` confirms all 5 hosts render correctly (4 APIs + UI). `terraform fmt` clean.
- **Helm gotcha:** `helm/middleware` has 26 microservice subchart references; must run `helm dependency build` before any `helm template` validation works.

### 2026-06-01 — .NET HTTP services onboarded to per-host ingress
- Onboarded the 2 .NET services with HTTP APIs (`dotnet-policy-issuance`, `dotnet-file-processing`) to the per-host ingress pattern; the other 7 .NET services are NServiceBus event-only and stay cluster-internal.
- Hostnames use `dotnet-` prefix to coexist with the Java per-host records (`policy.*`, `file-processing.*`) — Java + .NET addressable side-by-side via APIM for parity comparisons.
- `helm/middleware/values.yaml`: added `ingress: { enabled, internal, host, paths }` block to `dotnet-policy-issuance` and `dotnet-file-processing` only. Port (8181) and env untouched. No probes added.
- `infra/terraform/dns.tf`: added `azurerm_private_dns_a_record.dotnet_policy` and `.dotnet_file_processing` pointing to `local.ingress_ilb_ip` (10.0.16.10).
- `infra/terraform/network.tf`: extended the DNS-records comment block to enumerate the new hostnames.
- **Validation:** `helm template` renders both `host: dotnet-policy.middleware.internal` and `host: dotnet-file-processing.middleware.internal` (alongside the 4 Java hosts + ui/grafana/kafdrop). `terraform fmt -check -diff` clean.
- **Helm gotcha (still relevant):** had to `helm dependency update helm/middleware` first — `Chart.lock` was out of sync after subchart bumps.
- **APIM out of scope:** Azure agent owns APIM backend cutover to the new .NET hosts.
