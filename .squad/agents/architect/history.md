# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability — domain layer must not know about infrastructure technology
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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
