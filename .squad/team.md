# Squad Team

> Middleware

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Architect | Lead / Architect | .squad/agents/architect/charter.md | active |
| Integration | Integration Dev | .squad/agents/integration/charter.md | active |
| Azure | Azure Expert | .squad/agents/azure/charter.md | active |
| Platform | Platform Engineer | .squad/agents/platform/charter.md | active |
| Backend | Backend Dev | .squad/agents/backend/charter.md | active |
| Frontend | Frontend Dev | .squad/agents/frontend/charter.md | active |
| DevOps | Container / DevOps | .squad/agents/devops/charter.md | active |
| Grafana | Observability Eng. | .squad/agents/grafana/charter.md | active |
| QA | QA / Tester | .squad/agents/qa/charter.md | active |
| Scribe | Scribe | .squad/agents/scribe/charter.md | active |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | active |

## Project Context

- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability
- **Owner:** Steven Suing
- **Created:** 2026-05-25
- **Goals:** Functional correctness against Chubb BizTalk baseline; throughput/latency targets; observability (App Insights, Azure Monitor); security posture (Defender for Cloud, Key Vault, Managed Identities)
- **Local dev:** Rancher Desktop (Docker)
- **Logging:** Serilog
