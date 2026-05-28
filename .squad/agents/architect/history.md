# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability — domain layer must not know about infrastructure technology
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-27: EDA Events vs Commands — Udi Dahan Principles

**Directive from Steven Suing**: Udi Dahan is the authoritative source for EDA architectural guidance on this project. Consult his work for any event-driven architecture decisions.

**Core rule (from NServiceBus docs + Udi Dahan)**:
- Commands (`ICommand` / Kafka message to specific topic): Tell ONE service to do something. Have one logical owner. Are SENT directly. Cannot be published. Cannot have multiple handlers.
- Events (`IEvent` / Kafka message to pub/sub topic): Communicate that something happened. Have one logical PUBLISHER. Are PUBLISHED. Any service can SUBSCRIBE. Never sent directly.

**Pattern violated and fixed in UC1**:
- WRONG: `Publish(PolicyIssuanceInitiated)` then `Send(RequestComplianceCheckCommand)` to force compliance to check
- RIGHT: Publish `PolicyIssuanceInitiatedEvent` — Platform.Compliance subscribes autonomously
- WRONG: After PAS response, `Send(AssociateBillingAccountCommand)` and `Send(UpdateCustomerRecordCommand)`
- RIGHT: Platform.Integration publishes `PolicyAdminSystemResponseReceived` (fan-out) — Billing and Customer subscribe independently

**Udi Dahan's key insight**: "Services are autonomous. A service never commands another service — it publishes what happened and other services decide what to do. This enables loose coupling, parallel processing, and independent deployability."

**Kafka topic naming convention enforced**:
- Command topics: `{domain}.commands.{verb-noun}` (e.g., `policy.commands.issue-policy`) — direct, one consumer
- Event topics: `{domain}.events.{noun-verb-past-tense}` (e.g., `policy.events.policy-issuance-initiated`) — pub/sub, multiple consumers

**New events added in UC1 fix**:
- `PolicyIssuanceInitiatedEvent` — published by PolicyLifecycle, subscribed by Compliance
- `AccountLookupRequestedEvent` — published by PolicyLifecycle, subscribed by CustomerIdentity
- `PolicyAdminSystemResponseReceivedEvent` enriched with `accountServiceRequestNumber` for fan-out subscribers

**Fan-out pattern (pub/sub)**:
Platform.Integration publishes `PolicyAdminSystemResponseReceived` ONCE. THREE services subscribe independently:
1. PolicyIssuanceAndLifecycleManagement — updates issuance state
2. BillingAndFinanceManagement — associates billing account → publishes `BillingAssociationCreatedEvent`
3. CustomerIdentityAndRelationshipManagement — links customer to policy → publishes `CustomerUpdatedEvent`
PolicyLifecycle waits for both completion events (join pattern) before publishing `PolicyIssuedEvent`.

**2026-05-27 Extension**: EDA_FLOW observability now instruments both Java Camel and .NET NServiceBus stacks with structured logs (EDA_*MDC keys) to enable live sequence diagram visualization in the ops page. This creates an observable feedback loop where the diagram reflects actual message flow topology, reinforcing Udi Dahan's publish/subscribe semantics across both backends.

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

### 2026-05-28 — UC4 Appraisal Documents Architecture & Demo Gap Analysis

**Architecture Deliverable:**
- Created `.docs/demo-gaps-uc4.md` — comprehensive demo gap reference document identifying 9 requirements gaps (4 critical, 5 moderate)
- Gateway pattern enforcement validated: RiskIDMQGateway, PLUWGateway, PLAPRGateway, MasterpieceGateway, CustomerDBGateway — all designed as proper domain-layer abstractions with swappable adapter implementations
- Saga structure validated: AppraisalReceivedSaga (outer) → StatusCode6UWSaga, StatusCode15CompletedSaga, GenericStatusUpdateSaga (sub-workflows)
- Timeout handling design: Java/Camel uses custom timer + MongoDB state check; NServiceBus uses built-in `RequestTimeout<T>()`
- Join pattern for parallel calls (PLUW creation + UW determination) uses MongoDB `findAndModify()` atomic check (same pattern as UC1 issuance saga)

**Key Demo Gaps (Critical):**
1. UW Determination business rules — mocked with InspectionTypeCode proxy
2. PLAPR database schema — completely unknown
3. @Work MQ message format — completely unknown
4. Real RiskID sample payloads — simplified JSON instead of real MQ messages

**Architectural Principle Reinforced:**
- Every gap is isolated behind a gateway interface — resolving gaps requires adapter changes only, never saga logic changes
- This is the abstract-layer principle in action: design for unknowns

**Key File:** `.docs/demo-gaps-uc4.md`

### 2026-05-26 — AIS Stack Framing Correction

**Integration Boundary Framing:**
- Azure API Management, Blob Storage, Key Vault, App Configuration, Entra ID, Azure Monitor, and App Insights are shared platform services, not stack differentiators.
- When Kafka is already the production messaging backbone, Java + Camel removes the need for Azure Service Bus and Azure Logic Apps entirely.
- In Azure, .NET + NServiceBus typically adds Azure Service Bus as the transport; avoiding that means using SQL Server as message infrastructure instead.
- Logic Apps should be framed as a required adapter tier for .NET protocol gaps (SFTP, IBM MQ bridging, file polling, SQL polling), not as an optional convenience layer.

### 2026-05-28 — UC4 Cross-Stack Alignment & Gateway Pattern Enforcement

**Cross-Team Coordination Results:**
- All 6 agents (Architect, Backend, DotNet, Integration, Frontend, QA) completed UC4 Appraisal Documents in parallel with zero blocking dependencies
- Architecture pattern (gateway + orchestrator saga) applied identically across Java and .NET implementations
- Both Java `AppraisalReceivedSagaRoute` and .NET `AppraisalReceivedSaga` implement the same saga structure: outer orchestrator → StatusCode6UWSaga (parallel join), StatusCode15CompletedSaga, GenericStatusUpdateSaga
- All 5 gateways (RiskIDMQGateway, PLUWGateway, PLAPRGateway, MasterpieceGateway, CustomerDBGateway) use consistent domain/adapter separation across both stacks
- Demo gap visibility standard enforced: `⚠️ STUBBED` and `⚠️ DEMO GAP` markers visible in logs for every stub gateway call
- Frontend demo shell pattern makes requirements gaps explicit in UI with mock data flag + expandable gap panel
- QA two-section test standard (architecture patterns + demo gaps) enables honest stakeholder communication

**Gateway Pattern Enforcement Across Stacks:**
- Java: All 5 gateways defined as interfaces in `prs-appraisal-service/src/main/java/.../gateway/` (domain layer), implemented as adapter stubs in `src/main/java/.../gateway/adapter/`
- .NET: All 5 gateways defined as `I*Gateway` interfaces in `dotnet-prs-appraisal/Gateways/`, wired to static `AppraisalRuntime` class in `Program.cs`
- Consistent naming across stacks: `IRiskIDMQGateway`, `IPLUWGateway`, `IPLAPRGateway`, `IMasterpieceGateway`, `ICustomerDBGateway`
- All gateway stubs log with `⚠️ STUBBED:` or `⚠️ DEMO STUB:` prefix visible in docker logs during demo
- All fabricated data constants use `REPLACE_ME_*` naming for searchability in code review

**Parallel Join Pattern Alignment:**
- Java UC4 StatusCode6UWSagaRoute uses two separate Kafka subscriptions (`prs.events.pluw-appraisal-created` + `prs.events.uw-assignment-determined`) with MongoDB `findAndModify` CAS (status-based, like UC1 IssuanceSagaRoute)
- .NET UC4 AppraisalReceivedSaga implements equivalent parallel join using NServiceBus subscription to completion events
- Pattern reuse from UC1: atomic join condition via `findAndModify(returnNew(true))` + CAS prevents race condition on both stacks

**EDA Observability Contract Across Stacks:**
- Java: `AppraisalReceivedSagaRoute` uses existing `EDAFlowProcessor` intercepts; `appraisalId` mapped to `correlationId` fallback path
- .NET: Planned `EDAFlowBehavior` (NServiceBus pipeline) will emit same `EDA_*` MDC contract as Java
- Correlation key: `appraisalId` (UC4) stored as `correlationId` property/header so both observability systems pick it up without code duplication
- Live ops sequence diagram will render UC4 saga flow across Java/.NET choice at frontend runtime via `active-backend` cookie

**Demo Readiness Outcome:**
- UC4 dashboard is fully functional and demoable without appraisal-service backend implementation
- Frontend proxy tries real backend first, falls back to typed mock data with `isMockData: true` flag
- All 8 demo gap items documented in `.docs/demo-gaps-uc4.md` and searchable in code via `DEMO GAP`, `REPLACE_ME_*`, `⚠️ STUBBED`, `⚠️ DEMO GAP` markers
- Prep session can point to specific log lines showing stub boundaries (e.g., "This log from `⚠️ DEMO STUB: RiskIDMQGateway` is where the real IBM MQ message would arrive")
- QA test scenarios provide both architecture validation (patterns work) and explicit gap documentation for PRS team confirmation

**Key Learning: Design for Unknowns**
- When external system schemas are unknown, abstract them behind gateway interfaces — when the schema is confirmed, only the adapter changes, never the saga logic
- This applied to all 5 gateways: real IBM MQ schema, PLUW WCF contract, PLAPR stored procedure signature, Masterpiece Transaction 90 format, CustomerDB cross-reference structure
- The gateway pattern proved itself as the correct abstraction for UC4 because it isolated domain logic from infrastructure unknowns

**Cross-Stack Parity Achieved:**
- Both Java and .NET stacks now implement UC4 identically: orchestrator saga with gateway abstraction
- Frontend can switch between backends at runtime (`active-backend` cookie) without changing demo functionality
- QA architecture tests apply to both stacks (parallel join, saga state, timeout, EDA observability)
- Decision decisions #28-35 align all team members on the same architectural direction
