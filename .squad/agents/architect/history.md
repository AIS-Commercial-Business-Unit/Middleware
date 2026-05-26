# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability — domain layer must not know about infrastructure technology
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-26 — EIP Pattern Checklist Closes Technology Debate

**Positioning Guidance:**
- Once platform demonstrates all major BizTalk-replacement EIP patterns (dead letter queue, retry, pub/sub, scatter-gather, saga, idempotent consumer, outbox, content-based routing, message translation, data movement/ETL adapter flows), the technical viability argument is closed.
- Remaining delivery risk must be framed as organizational: program management, migration sequencing, CI/CD maturity, team enablement, and governance.
- This repo is proof that patterns work; professional services is the mechanism for migrating 67+ real applications without collapse.
- CI/CD must be a prerequisite for integration modernization, not a follow-on implementation detail.

**AIS Stack Framing Correction:**
- Azure APIM, Blob Storage, Key Vault, App Configuration, Entra ID, Monitor, App Insights are shared platform services, NOT stack differentiators.
- With Kafka as the production backbone, Java + Camel removes Azure Service Bus and Logic Apps entirely.
- .NET + NServiceBus typically adds Azure Service Bus or SQL Server as message infrastructure; Logic Apps bridges protocol gaps Camel handles natively.
- The Java/Camel path is architecturally simpler because it preserves shared Azure services while removing operational sprawl.

### 2026-05-26 — Pattern Proof vs. Program Risk Framing

**Positioning Guidance:**
- Once the platform demonstrates the major BizTalk-replacement EIP patterns end-to-end, the technical viability argument is closed.
- The remaining delivery risk should be framed as organizational: program management, migration sequencing, CI/CD maturity, team enablement, and governance.
- This repo should be described as proof that the patterns work; professional services is the mechanism that gets 67+ real applications migrated without collapse.
- CI/CD must be treated as a prerequisite for integration modernization, not as follow-on implementation detail.


### 2026-05-25 — Architecture Sweep

**DDD Layer Separation Pattern:**
- Domain entities MUST be clean Java — no Spring, MongoDB, or Camel annotations
- Infrastructure lives in `persistence/` subpackage with:
  - `*Document.java` — database-annotated entities
  - `*MongoRepository.java` — Spring Data interfaces
  - `*RepositoryAdapter.java` — implements domain interface, handles mapping
- Domain repositories are pure Java interfaces (e.g., `Optional<T> findById(String id)`)

**DLQ Convention:**
- Every Camel route must have `onException` handler with DLQ
- Topic pattern: `{domain}.dlq.{route-name}-failures`
- Standard policy: 3 retries, exponential backoff, then DLQ

**Event Schema Versioning:**
- `VersionedEvent` and `VersionedCommand` interfaces added to common
- Events should implement these for forward/backward compatibility
- Schema version starts at "1.0", increment major on breaking changes

**Naming Conventions Verified:**
- Commands: `*Command` suffix (e.g., `IssuePolicyCommand`, `AssociateBillingAccountCommand`)
- Events: `*Event` suffix (e.g., `PolicyIssuedEvent`, `ComplianceClearedEvent`)
- All current events/commands follow convention

**Coordination Results:**
- UC1 verified end-to-end after sweep
- UC3 verified end-to-end after sweep
- All DDD violations eliminated; domain layer now completely infrastructure-free
- Orchestration log: `.squad/orchestration-log/2026-05-26T01-33-25Z-architect-1.md`

### 2026-05-26 — Java vs .NET BizTalk Replacement Analysis

**Intel Findings:**
- Client has 67+ BizTalk applications across 4 portfolios: SCI (18), PRS/RiskID (19), ClaimCare (30), ECOS (4+)
- BizTalk adapters in use: WCF-SQL, WCF-WebHTTP, WCF-BasicHTTP, WCF-WSHTTP, SFTP, FILE, MSMQ, MQSC (IBM MQ), DB2
- Sanctions system: 18K transactions/day/region across EMEA, APAC, LATAM; 24/7 availability; uses IBM MQ and SOAP/CLink
- ECOS: SQL polling at 10-12K txn/hour throughput
- Renewal batch: 40-60K records nightly; currently sequential (performance bottleneck)
- PAS integrations: DuckCreek, ForeFront, Insurity — all SOAP/WCF based
- PRS uses IBM MQ heavily for RiskID and Appraisal workflows

**Decision Rationale:**
- Apache Camel provides native, production-tested components for every adapter pattern found in client BizTalk environment
- Client team already operates Java, Kafka, MongoDB, Docker — zero retraining required
- .NET/NServiceBus would require Logic Apps for adapter gaps (IBM MQ, SFTP, File), commercial licensing, and full team retraining
- NServiceBus has superior saga primitives but this doesn't outweigh the operational and licensing burden
- The real engagement value is program management (discovery, architecture, migration, testing, change management) not platform adoption

**Output:** `.docs/java-vs-dotnet-biztalk-replacement.md` — full strategic comparison document
**Decision:** `.squad/decisions/inbox/architect-java-vs-dotnet-recommendation.md`

### 2026-05-26 — AIS Stack Framing Correction

**Integration Boundary Framing:**
- Azure API Management, Blob Storage, Key Vault, App Configuration, Entra ID, Azure Monitor, and App Insights are shared platform services, not stack differentiators.
- When Kafka is already the production messaging backbone, Java + Camel removes the need for Azure Service Bus and Azure Logic Apps entirely.
- In Azure, .NET + NServiceBus typically adds Azure Service Bus as the transport; avoiding that means using SQL Server as message infrastructure instead.
- Logic Apps should be framed as a required adapter tier for .NET protocol gaps (SFTP, IBM MQ bridging, file polling, SQL polling), not as an optional convenience layer.
