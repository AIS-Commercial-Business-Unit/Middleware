# AIS Middleware Platform

A dual-stack enterprise middleware platform demonstrating how to replace legacy BizTalk with a modern, cloud-native event-driven architecture. The same business logic is implemented side-by-side in two technology stacks so you can compare, contrast, and choose the approach that best fits your team.

## Purpose

The patterns demonstrated here — publish/subscribe, saga/long-running process, dead letter queues, idempotent consumers, scatter-gather, outbox, content-based routing, and data movement — are the same patterns that appear in every BizTalk modernization engagement. Once a platform demonstrates these patterns work end-to-end, the technology question is answered.

**What remains is not a technology problem. It is an organizational one:**

- Program management: who is sequencing the migration of dozens of applications across teams that have other work to do?
- Discovery: is the real BizTalk estate (including informal customizations) fully inventoried?
- CI/CD: does the organization have the pipeline maturity to deploy and test integration changes independently?
- Enablement: are the teams trained on event-driven patterns and the new operational model?
- Governance: who owns the domain boundaries when two teams' applications overlap?

The platform answers "can we?" The professional services engagement answers "will we, on time, without the program falling apart."

---

## What Is This?

This repository contains a fully runnable local demo of an enterprise integration platform built on:

| Concern | Java Stack | .NET Stack |
|---|---|---|
| Integration framework | Apache Camel 4.x | NServiceBus 8.x (Particular Software) |
| Messaging | Kafka | SQL Server transport (Kafka bridge for shared topics) |
| Domain persistence | MongoDB | MongoDB |
| Application container | Spring Boot 3.3 / Java 21 | ASP.NET Core / .NET 8 |
| Structured logging | SLF4J + Logback JSON | Serilog + OpenTelemetry |
| Observability | Grafana + Loki + Tempo + Prometheus | Same Grafana stack |

Both stacks share:
- **Platform UI** (Next.js 15) — switchable between backends
- **MongoDB** — domain state for sagas
- **Kafka** — shared event topics via bridge service
- **Grafana** — unified observability dashboard

---

## Demo Use Cases

| Use Case | What It Shows |
|---|---|
| **UC1 — Policy Issuance** | Long-running saga coordinating compliance, PAS routing, billing, and notification across 6 services |
| **UC3 — Automated Renewal Batch** | File-based batch processing: CSV pickup → transform → saga dispatch → policy issuance |

---

## Documentation

### Getting Started

| Guide | Description |
|---|---|
| 📖 [Platform Concepts](.docs/getting-started.md) | How Kafka, sagas, DLQ, idempotency, and EIPs fit together — read this first |
| ☕ [Getting Started — Java Stack](.docs/getting-started-java.md) | Run the Apache Camel / Spring Boot stack locally |
| 🔷 [Getting Started — .NET Stack](.docs/getting-started-dotnet.md) | Run the NServiceBus / ASP.NET Core stack locally |
| 🎬 [Running the Demos](.docs/running-the-demos.md) | Tech-agnostic walkthrough of UC1 and UC3 — what to look for |

### Technical References

| Document | Description |
|---|---|
| 🧪 [Testability Comparison](.docs/testing-comparison.md) | Apache Camel vs NServiceBus — unit testing sagas and routes |
| 📊 [Java vs .NET — Strategy & Recommendation](.docs/java-vs-dotnet-biztalk-replacement.md) | Deep-dive comparison for BizTalk replacement: adapter coverage, EIP patterns, operational fit, and final recommendation |

---

## Quick Start

> **Prerequisite:** [Rancher Desktop](https://rancherdesktop.io/) or Docker Desktop must be running.

```bash
# Clone the repository
git clone https://github.com/AIS-Commercial-Business-Unit/Middleware.git
cd Middleware

# Start the full stack (both Java and .NET)
docker compose up --build

# Run the end-to-end test suite
.\scripts\test-e2e.ps1 -Stack both -Verbose
```

**Key URLs once running:**

| Service | URL |
|---|---|
| Platform UI | http://localhost:3000 |
| Kafdrop (Kafka UI) | http://localhost:9000 |
| Mongo Express | http://localhost:8888 |
| Grafana (admin / admin) | http://localhost:3001 |
| Prometheus | http://localhost:9090 |

---

## Particular Service Platform (ServicePulse)

The `.NET` stack integrates with [Particular ServicePulse](https://particular.net/servicepulse) for message monitoring, heartbeats, and failed message management. It requires a separate compose overlay and a `License.xml` file from Particular Software.

**Normal start** (main stack + ServicePulse):
```bash
docker compose -f docker-compose.yml -f docker-compose.particular.yml up -d
```

**Full clean reset** (deletes RavenDB data — use only when you need a fresh ServiceControl state):
```bash
docker compose -f docker-compose.yml -f docker-compose.particular.yml down -v --remove-orphans
docker compose -f docker-compose.yml -f docker-compose.particular.yml up -d
```

**ServicePulse URLs:**

| Service | URL |
|---|---|
| ServicePulse UI | http://localhost:9091 |
| ServiceControl API | http://localhost:33333/api |
| ServiceControl Audit API | http://localhost:44445/api |
| ServiceControl Monitoring API | http://localhost:33633/monitoring-api |
| RavenDB (ServiceControl storage) | http://localhost:8080 |

**Validation commands:**
```bash
docker compose -f docker-compose.yml -f docker-compose.particular.yml ps
docker compose -f docker-compose.yml -f docker-compose.particular.yml logs servicepulse
docker compose -f docker-compose.yml -f docker-compose.particular.yml logs servicecontrol
docker compose -f docker-compose.yml -f docker-compose.particular.yml logs servicecontrol-monitoring
docker compose -f docker-compose.yml -f docker-compose.particular.yml logs servicecontrol-audit
```

---

## Architecture Overview

```
                        ┌─────────────────────────────────┐
                        │         Platform UI              │
                        │   (Next.js 15 — port 3000)       │
                        │  Saga Explorer · Event Stream    │
                        └────────────┬────────────────────┘
                                     │ HTTP
                    ┌────────────────┴────────────────┐
                    │                                 │
          ┌─────────▼──────────┐         ┌──────────▼──────────┐
          │     Java Stack     │         │    .NET Stack        │
          │  Apache Camel 4.x  │         │  NServiceBus 8.x     │
          │  Spring Boot 3.3   │         │  ASP.NET Core .NET 8 │
          │  Ports 8081–8087   │         │  Ports 8181–8188     │
          └─────────┬──────────┘         └──────────┬──────────┘
                    │                               │
                    └──────────┬────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
        ┌─────▼─────┐   ┌──────▼──────┐  ┌──────▼──────┐
        │   Kafka   │   │   MongoDB   │  │ SQL Server  │
        │  (shared) │   │  (shared)   │  │ (.NET only) │
        └─────┬─────┘   └─────────────┘  └─────────────┘
              │
        ┌─────▼─────────────────────────────────┐
        │            Observability               │
        │  Grafana · Loki · Tempo · Prometheus  │
        └────────────────────────────────────────┘
```

---

## Repository Structure

```
Middleware/
├── java/                          ← Java/Apache Camel stack
│   ├── common/                    ←   Shared domain event POJOs
│   ├── policy-issuance-service/   ←   UC1 saga + REST entry point
│   ├── platform-compliance-service/
│   ├── customer-identity-service/
│   ├── platform-integration-service/
│   ├── billing-finance-service/
│   ├── platform-notification-service/
│   └── stubs/                     ←   Fake external systems (7 stubs)
│
├── dotnet/                        ← .NET/NServiceBus stack
│   ├── dotnet-policy-issuance/    ←   UC1 NServiceBus saga
│   ├── dotnet-platform-compliance/
│   ├── dotnet-customer-identity/
│   ├── dotnet-platform-integration/
│   ├── dotnet-billing-finance/
│   ├── dotnet-platform-notification/
│   ├── dotnet-file-processing/    ←   UC3 batch file processor
│   ├── dotnet-kafka-bridge/       ←   Forwards .NET events to Kafka
│   └── tests/                     ←   Unit tests (NServiceBus.Testing)
│
├── platform-ui/                   ← Next.js 15 (backend-switchable)
├── observability/                 ← Grafana, Loki, Tempo, Prometheus configs
├── scripts/                       ← test-e2e.ps1 and helpers
├── docker-compose.yml             ← Full stack orchestration
└── .docs/                         ← Documentation
```

---

## Design Principles

1. **Abstract layer first** — requirements and design are technology-neutral; either stack can be swapped out
2. **Domain-Driven Design** — bounded contexts, domain events, no shared database schemas
3. **Event-driven** — services communicate via events, never direct calls
4. **Anti-Corruption Layer** — domain services never call external systems directly; integration services own that boundary
5. **Observable by default** — every service emits structured logs, metrics, and distributed traces
6. **Runnable locally** — the entire platform starts with `docker compose up --build`
