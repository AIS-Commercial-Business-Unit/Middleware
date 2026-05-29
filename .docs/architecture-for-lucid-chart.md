# AIS Middleware Platform — Architecture Reference for Lucid Chart

> Use this document as an AI prompt in Lucid Chart to generate a comprehensive architecture diagram showing the full dual-stack (Java + .NET) BizTalk replacement platform.

---

## Platform Overview

This is an **event-driven middleware platform** replacing IBM BizTalk Server. It runs **two parallel technology stacks** (Java/Apache Camel and .NET/NServiceBus) demonstrating that both can implement the same integration patterns. The platform processes insurance policy issuance, file-based renewal batches, and property appraisal document workflows via Kafka pub/sub messaging, with MongoDB for saga state and SQL Server for NServiceBus transport.

---

## Running Components — Complete List

### Infrastructure Layer (Shared)

| Component | Technology | Port | Purpose |
|-----------|-----------|------|---------|
| **Zookeeper** | Confluent CP 7.7 | 2181 | Kafka cluster coordination |
| **Kafka** | Confluent CP 7.7 | 9092 | Event backbone — all inter-service messaging |
| **Kafka Setup** | Confluent CP 7.7 | — | Topic provisioning (40+ topics, runs once at startup) |
| **Kafka UI (Kafdrop)** | Kafdrop 4.0.2 | 9000 | Kafka topic inspection and message browsing |
| **MongoDB** | MongoDB 7.0 | 27017 | Saga state persistence, domain data (8 databases) |
| **Mongo Express** | Mongo Express 1.0.2 | 8888 | MongoDB web admin UI |
| **SQL Server** | MSSQL 2022 | 1433 | NServiceBus transport (message queuing for .NET stack) |
| **SQL Server Init** | MSSQL 2022 | — | Creates `middleware_nsb` database + subscription routing table |

### Observability Layer (Shared)

| Component | Technology | Port | Purpose |
|-----------|-----------|------|---------|
| **OpenTelemetry Collector** | OTel Contrib 0.111 | 4317/4318 (internal) | Trace + metric + log pipeline aggregation |
| **Grafana Tempo** | Tempo 2.6 | 3200 | Distributed trace storage (receives from OTel Collector) |
| **Grafana Loki** | Loki 3.2 | 3100 | Log aggregation (receives from Promtail) |
| **Prometheus** | Prometheus 2.54 | 9090 | Metrics scraping and storage |
| **Grafana** | Grafana 11.2 | 3001 | Unified dashboards (traces + logs + metrics) |
| **Promtail** | Promtail 3.2 | — | Docker log shipping to Loki |

### External System Stubs (Java — Shared by Both Stacks)

| Component | Technology | Port | Simulates |
|-----------|-----------|------|-----------|
| **DuckCreek Commercial Stub** | Java/Spring Boot | 9001 | DuckCreek commercial lines PAS |
| **DuckCreek Personal Stub** | Java/Spring Boot | 9002 | DuckCreek personal lines PAS |
| **ForeFront Stub** | Java/Spring Boot | 9003 | Insurity ForeFront PAS |
| **RSK3X3 Compliance Stub** | Java/Spring Boot | 9004 | Compliance screening system |
| **ERM7X1 Account Stub** | Java/Spring Boot | 9005 | Account service system |
| **CRM40X1 Customer Stub** | Java/Spring Boot | 9006 | Customer relationship system |
| **CRM19X1 Billing Stub** | Java/Spring Boot | 9007 | Billing association system |

### Java Stack — Domain Services

| Component | Technology | Port | Bounded Context |
|-----------|-----------|------|-----------------|
| **policy-issuance-service** | Java 21 / Spring Boot / Apache Camel | 8081 | Policy Issuance & Lifecycle Management |
| **platform-compliance-service** | Java 21 / Spring Boot / Apache Camel | 8082 | Compliance Screening |
| **customer-identity-service** | Java 21 / Spring Boot / Apache Camel | 8083 | Customer Identity & Relationship + Producer Lookup (UC4) |
| **platform-integration-service** | Java 21 / Spring Boot / Apache Camel | 8084 | External PAS Integration (DuckCreek, ForeFront) |
| **billing-finance-service** | Java 21 / Spring Boot / Apache Camel | 8085 | Billing & Finance Management |
| **platform-notification-service** | Java 21 / Spring Boot / Apache Camel | 8086 | Notification Dispatch (SignalR relay) |
| **platform-file-processing-service** | Java 21 / Spring Boot / Apache Camel | 8087 | File Polling & Batch Processing (UC3) |
| **prs-appraisal-service** | Java 21 / Spring Boot / Apache Camel | 8090 | PRS Appraisal Document Processing (UC4) |

### .NET Stack — Domain Services

| Component | Technology | Port | Bounded Context |
|-----------|-----------|------|-----------------|
| **dotnet-policy-issuance** | .NET 8 / NServiceBus | 8181 | Policy Issuance & Lifecycle Management |
| **dotnet-platform-compliance** | .NET 8 / NServiceBus | 8182 | Compliance Screening |
| **dotnet-customer-identity** | .NET 8 / NServiceBus | 8183 | Customer Identity & Relationship |
| **dotnet-platform-integration** | .NET 8 / NServiceBus | 8184 | External PAS Integration |
| **dotnet-billing-finance** | .NET 8 / NServiceBus | 8185 | Billing & Finance Management |
| **dotnet-platform-notification** | .NET 8 / NServiceBus | 8186 | Notification Dispatch |
| **dotnet-file-processing** | .NET 8 / NServiceBus | 8187 | File Polling & Batch Processing (UC3) |
| **dotnet-kafka-bridge** | .NET 8 / NServiceBus | 8188 | Kafka↔NServiceBus message translation |
| **dotnet-prs-appraisal** | .NET 8 / NServiceBus | 8189 | PRS Appraisal Document Processing (UC4) |

### Frontend / UI Layer

| Component | Technology | Port | Purpose |
|-----------|-----------|------|---------|
| **platform-ui** | React 19 / Next.js 15 / TypeScript | 3000 | Unified operations dashboard — backend-switchable via cookie |

---

## Component Relationships & Communication

### Messaging Patterns

```
┌─────────────────────────────────────────────────────────────────────┐
│                         KAFKA (Event Backbone)                       │
│  Topics organized by domain: policy.*, compliance.*, customer.*,     │
│  billing.*, integration.*, notification.*, file.*, prs.*            │
└───────────┬────────────────────────────────────────────┬────────────┘
            │ Java services produce/consume directly      │
            ▼                                             ▼
┌───────────────────────┐                   ┌──────────────────────────┐
│  Java Domain Services  │                   │  dotnet-kafka-bridge     │
│  (Apache Camel routes) │                   │  (Kafka↔NServiceBus)     │
└───────────────────────┘                   └──────────┬───────────────┘
                                                       │ NServiceBus messages
                                                       ▼
                                            ┌──────────────────────────┐
                                            │  SQL Server Transport    │
                                            │  (middleware_nsb DB)     │
                                            └──────────┬───────────────┘
                                                       │
                                                       ▼
                                            ┌──────────────────────────┐
                                            │  .NET Domain Services    │
                                            │  (NServiceBus handlers)  │
                                            └──────────────────────────┘
```

### Inter-Service Communication Matrix

| From → To | Protocol | Medium |
|-----------|----------|--------|
| Java service → Java service | Kafka pub/sub | Kafka topics |
| .NET service → .NET service | NServiceBus pub/sub | SQL Server transport |
| Java stack ↔ .NET stack | Kafka bridge | dotnet-kafka-bridge translates |
| Java service → External stub | REST/HTTP | Direct HTTP call |
| .NET service → External stub | REST/HTTP | Direct HTTP call |
| Java service → MongoDB | MongoDB driver | TCP 27017 |
| .NET service → MongoDB | MongoDB driver | TCP 27017 |
| .NET service → SQL Server | ADO.NET / NServiceBus | TCP 1433 |
| All services → OTel Collector | OTLP gRPC | TCP 4317 |
| platform-ui → Java backend | REST API proxy | HTTP |
| platform-ui → .NET backend | REST API proxy | HTTP |
| Promtail → Loki | HTTP push | TCP 3100 |
| Grafana → Tempo/Loki/Prometheus | HTTP query | TCP 3200/3100/9090 |

---

## Service Boundaries — What Each Service Owns

### Policy Issuance & Lifecycle Management (policy-issuance-service / dotnet-policy-issuance)
- **Owns:** Issuance saga orchestration, policy lifecycle state
- **MongoDB:** `policy_issuance_db` — collections: `issuance_sagas`, `policy_records`
- **Kafka Topics:** `policy.commands.issue-policy`, `policy.events.policy-issuance-initiated`, `policy.events.policy-issued`, `policy.events.issuance-failed`, `policy.events.issue-policy-requested`
- **Pattern:** Saga orchestrator — waits for compliance + billing + customer completion (join pattern)

### Compliance Screening (platform-compliance-service / dotnet-platform-compliance)
- **Owns:** Compliance check execution, screening state
- **MongoDB:** `compliance_db` — collections: `compliance_checks`
- **Kafka Topics:** `compliance.commands.request-compliance-check`, `compliance.events.compliance-cleared`, `compliance.events.compliance-blocked`
- **External:** RSK3X3 compliance stub (REST)

### Customer Identity & Relationship (customer-identity-service / dotnet-customer-identity)
- **Owns:** Account service record lookup, customer record updates, producer cross-reference lookup (UC4)
- **MongoDB:** `customer_identity_db`
- **Kafka Topics:** `customer.events.account-lookup-requested`, `customer.commands.get-or-create-account-record`, `customer.events.account-service-record-retrieved`, `customer.commands.update-customer-record`, `customer.events.customer-updated`, `prs.events.producer-lookup-requested`, `prs.events.producer-crossref-retrieved`
- **External:** ERM7X1 account stub, CRM40X1 customer stub
- **DLQ:** `customer.dlq.producer-lookup`

### Platform Integration (platform-integration-service / dotnet-platform-integration)
- **Owns:** External PAS system calls, response translation
- **MongoDB:** `integration_db`
- **Kafka Topics:** `integration.events.policy-admin-system-response-received`, `integration.events.policy-admin-system-call-failed`
- **External:** DuckCreek Commercial, DuckCreek Personal, ForeFront (all via REST stubs)
- **Pattern:** Content-based routing — routes to correct PAS based on line of business

### Billing & Finance (billing-finance-service / dotnet-billing-finance)
- **Owns:** Billing account association
- **MongoDB:** `billing_finance_db`
- **Kafka Topics:** `billing.commands.associate-billing-account`, `billing.events.billing-association-created`
- **External:** CRM19X1 billing stub (REST)

### Notification Dispatch (platform-notification-service / dotnet-platform-notification)
- **Owns:** Notification routing and delivery
- **MongoDB:** `notification_db`
- **Kafka Topics:** `notification.commands.publish-notification-intent`, `notification.events.notification-dispatched`
- **Future:** Azure SignalR Service relay for real-time UI push

### File Processing (platform-file-processing-service / dotnet-file-processing)
- **Owns:** File polling, CSV parsing, batch orchestration
- **MongoDB:** `file_processing_db` — collections: `file_batches`, `batch_records`
- **Kafka Topics:** `file.events.file-batch-started`, `file.events.file-batch-progress-updated`, `file.events.file-batch-completed`, `file.events.file-batch-partial-failure`, `file.events.renewal-record-ready-for-issuance`, `policy.events.renewal-record-processed`, `policy.events.renewal-record-failed`
- **Pattern:** File polling → CSV split → per-record Kafka emit → join for batch completion

### PRS Appraisal Documents (prs-appraisal-service / dotnet-prs-appraisal)
- **Owns:** Appraisal received saga, underwriting determination, PLUW/PLAPR/Masterpiece integration
- **MongoDB:** `prs_appraisal_db` — collection: `appraisal_received_sagas`
- **Kafka Topics:** `prs.events.appraisal-received`, `prs.events.producer-lookup-requested`, `prs.events.producer-crossref-retrieved`, `prs.events.pluw-appraisal-create-requested`, `prs.events.pluw-appraisal-created`, `prs.events.uw-determination-requested`, `prs.events.uw-assignment-determined`, `prs.events.appraisal-uw-assigned`, `prs.events.appraisal-completed`, `prs.events.appraisal-status-update-failed`
- **DLQ:** `prs.dlq.appraisal-saga-failures`
- **Pattern:** Orchestrator saga → parallel join (PLUW + UW determination) → downstream update
- **Gateways (abstracted):** RiskIDMQGateway (IBM MQ inbound), PLUWGateway (@Work WCF), PLAPRGateway (SQL stored proc), MasterpieceGateway (Transaction 90), CustomerDBGateway (producer lookup)

---

## Event Flows — Key Use Cases

### UC1: Policy Issuance (Synchronous Saga)

```
API Request → policy-issuance-service
  → publishes PolicyIssuanceInitiatedEvent
    → platform-compliance-service subscribes → calls RSK3X3 → publishes ComplianceClearedEvent
    → customer-identity-service subscribes → calls ERM7X1 → publishes AccountServiceRecordRetrievedEvent
  → policy-issuance-service receives compliance+account → publishes IssuePolicyRequestedEvent
    → platform-integration-service subscribes → calls DuckCreek/ForeFront → publishes PolicyAdminSystemResponseReceivedEvent
      → billing-finance-service subscribes → calls CRM19X1 → publishes BillingAssociationCreatedEvent
      → customer-identity-service subscribes → calls CRM40X1 → publishes CustomerUpdatedEvent
  → policy-issuance-service receives billing+customer → publishes PolicyIssuedEvent
    → platform-notification-service subscribes → dispatches notification
```

### UC3: Renewal File Batch Processing

```
File arrives in /inbound → platform-file-processing-service
  → publishes FileBatchStartedEvent
  → splits CSV → per-record publishes RenewalRecordReadyForIssuanceEvent
    → policy-issuance-service subscribes → triggers UC1 flow per record
    → publishes RenewalRecordProcessedEvent / RenewalRecordFailedEvent
  → file-processing joins results → publishes FileBatchCompletedEvent / FileBatchPartialFailureEvent
```

### UC4: Appraisal Document Processing (PRS)

```
RiskID MQ message (HTTP stub in demo) → prs-appraisal-service
  → publishes AppraisalReceivedEvent
  → Saga routes by statusCode:
    StatusCode=6 (New Inspection):
      → publishes ProducerLookupRequestedEvent
        → customer-identity-service subscribes → stub lookup → publishes ProducerCrossReferenceRetrievedEvent
      → publishes PLUWAppraisalCreateRequestedEvent (parallel)
      → publishes UWDeterminationRequestedEvent (parallel)
      → PARALLEL JOIN: waits for PLUWAppraisalCreatedEvent + UWAssignmentDeterminedEvent
      → publishes AppraisalUWAssignedEvent
    StatusCode=15 (Completed):
      → calls MasterpieceGateway (Transaction 90) → publishes AppraisalCompletedEvent
    Other StatusCodes:
      → calls PLAPRGateway → publishes generic status update
```

---

## Architecture Layers (DDD Hexagonal)

```
┌──────────────────────────────────────────────────────────────────┐
│                        API / Transport Layer                       │
│  REST Controllers (Spring MVC / ASP.NET)                         │
│  Kafka Consumers (Camel Routes / NServiceBus Handlers)           │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                      Domain Layer (PURE)                          │
│  Entities (AppraisalSagaRecord, IssuanceSaga, etc.)             │
│  Gateway Interfaces (PLUWGateway, MasterpieceGateway, etc.)     │
│  Repository Interfaces (AppraisalSagaRepository, etc.)          │
│  Value Objects, Enums, Domain Events                             │
│  ⚠️ NO infrastructure imports (no Spring, MongoDB, Camel, NServiceBus) │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Infrastructure / Adapter Layer                  │
│  MongoDB Adapters (*RepositoryAdapter.java / *Document.java)     │
│  Gateway Stubs (Stub*.java / *GatewayStub.cs)                   │
│  Kafka Producer/Consumer Config                                   │
│  NServiceBus Saga Persistence (MongoDB)                          │
│  OpenTelemetry Instrumentation                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## Infrastructure — Production Azure Services (Not in Docker)

| Azure Service | Purpose | Used By |
|---------------|---------|---------|
| **Azure Kubernetes Service (AKS)** | Container orchestration (production) | All services |
| **Azure API Management (APIM)** | External API gateway, rate limiting, auth | Frontend, external consumers |
| **Azure Key Vault** | Secret management | All services (connection strings, API keys) |
| **Azure App Configuration** | Feature flags, config values | All services |
| **Azure Blob Storage** | File staging (renewal CSVs, appraisal docs) | File processing services |
| **Azure SignalR Service** | Real-time push notifications to UI | Notification service → platform-ui |
| **Azure Monitor + App Insights** | Production observability (replaces local Grafana stack) | All services |
| **Microsoft Entra ID** | Managed identities, service auth | All services (zero-secret auth) |

---

## Kafka Topic Organization

### Domain: `policy.*`
- `policy.commands.issue-policy` — Command: initiate issuance
- `policy.events.policy-issuance-initiated` — Event: issuance started
- `policy.events.issue-policy-requested` — Event: ready for PAS call
- `policy.events.policy-issued` — Event: issuance complete
- `policy.events.issuance-failed` — Event: issuance failed
- `policy.events.renewal-record-processed` — Event: batch record done
- `policy.events.renewal-record-failed` — Event: batch record failed

### Domain: `compliance.*`
- `compliance.commands.request-compliance-check` — Command
- `compliance.events.compliance-cleared` — Event
- `compliance.events.compliance-blocked` — Event

### Domain: `customer.*`
- `customer.events.account-lookup-requested` — Event
- `customer.commands.get-or-create-account-record` — Command
- `customer.events.account-service-record-retrieved` — Event
- `customer.commands.update-customer-record` — Command
- `customer.events.customer-updated` — Event
- `customer.dlq.producer-lookup` — Dead Letter Queue

### Domain: `billing.*`
- `billing.commands.associate-billing-account` — Command
- `billing.events.billing-association-created` — Event

### Domain: `integration.*`
- `integration.events.policy-admin-system-response-received` — Event
- `integration.events.policy-admin-system-call-failed` — Event

### Domain: `notification.*`
- `notification.commands.publish-notification-intent` — Command
- `notification.events.notification-dispatched` — Event

### Domain: `file.*`
- `file.events.renewal-record-ready-for-issuance` — Event
- `file.events.file-batch-started` — Event
- `file.events.file-batch-progress-updated` — Event
- `file.events.file-batch-completed` — Event
- `file.events.file-batch-partial-failure` — Event

### Domain: `prs.*` (UC4 Appraisal)
- `prs.events.appraisal-received` — Event: inbound from RiskID
- `prs.events.producer-lookup-requested` — Event
- `prs.events.producer-crossref-retrieved` — Event
- `prs.events.pluw-appraisal-create-requested` — Event
- `prs.events.pluw-appraisal-created` — Event
- `prs.events.uw-determination-requested` — Event
- `prs.events.uw-assignment-determined` — Event
- `prs.events.appraisal-uw-assigned` — Event
- `prs.events.appraisal-completed` — Event
- `prs.events.appraisal-status-update-failed` — Event
- `prs.dlq.appraisal-saga-failures` — Dead Letter Queue

---

## MongoDB Databases

| Database | Service Owner | Key Collections |
|----------|--------------|-----------------|
| `policy_issuance_db` | policy-issuance-service | issuance_sagas, policy_records |
| `compliance_db` | platform-compliance-service | compliance_checks |
| `customer_identity_db` | customer-identity-service | account_records |
| `integration_db` | platform-integration-service | integration_logs |
| `billing_finance_db` | billing-finance-service | billing_associations |
| `notification_db` | platform-notification-service | notifications |
| `file_processing_db` | platform-file-processing-service | file_batches, batch_records |
| `prs_appraisal_db` | prs-appraisal-service | appraisal_received_sagas |

---

## Stack Attribution Summary

| Category | Java Stack | .NET Stack | Shared |
|----------|-----------|-----------|--------|
| Domain Services | 8 services | 9 services (includes kafka-bridge) | — |
| Messaging | Apache Camel + Kafka | NServiceBus + SQL Server transport | Kafka (backbone) |
| Saga Persistence | MongoDB (findAndModify CAS) | MongoDB (NServiceBus saga persister) | MongoDB |
| Message Transport | Kafka (direct) | SQL Server + Kafka Bridge | — |
| External Stubs | 7 Spring Boot stubs | — | Shared by both stacks |
| Observability | OTel Java agent | OTel .NET SDK | OTel Collector, Grafana, Loki, Tempo, Prometheus |
| Frontend | — | — | React 19 / Next.js 15 (backend-switchable) |
| Infrastructure | — | — | Kafka, MongoDB, SQL Server, Zookeeper |

---

## Diagram Suggestions for Lucid Chart

1. **Top-level view:** Show two vertical lanes (Java left, .NET right) with shared infrastructure in the center
2. **Kafka as horizontal bus** connecting both lanes through the center
3. **dotnet-kafka-bridge** as the translation point between Kafka and NServiceBus
4. **External stubs** at the bottom/right as leaf nodes called by both stacks
5. **Observability** as a horizontal layer above everything (OTel Collector fan-out to Tempo/Loki/Prometheus → Grafana)
6. **platform-ui** at the very top with arrows to both Java and .NET backends (switchable)
7. **MongoDB** shared between both stacks (center), **SQL Server** .NET-only (right lane)
8. **Azure cloud services** as a cloud boundary around the entire system for production deployment context
9. **Color coding:** Java = blue, .NET = purple, Shared = gray, External = orange, Azure = Azure blue
