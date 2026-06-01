# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability — domain layer must not know about infrastructure technology
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-31: EDA Compliance Review — UC4 Scatter-Gather Pattern

**Finding:** The UC4 `DocumentListSaga` scatter-gather is architecturally incomplete. The AtWork path is synchronous inline (temporal coupling) while the mainframe path is correctly event-driven. The requirements define a published-event fan-out pattern (§9.2, §10) where `AppraisalDocumentListRequested` triggers independent handlers — this is not implemented. The event contracts exist (`Uc4AppraisalDocumentListRequestedEvent`, `Uc4AtWorkDocumentListCompletedEvent`) but are dead code.

**Key Principle Reinforced:** In NServiceBus scatter-gather, the coordinator saga must ONLY publish an event and wait for completion events. It must NEVER call services inline or send direct commands to known participants. This is the Udi Dahan separation: the saga doesn't know who will respond, only that responses will arrive.

**Action:** Decision inbox entry filed (`architect-eda-review-uc4.md`) with 3 critical, 3 important, and 3 minor findings. Primary fix is a single refactor pass: publish event → create AtWork handler → convert mainframe aggregator from command to event subscription → saga waits for both completion events.

### 2026-05-29: UC4 Architecture Sweep & Decisions Finalization

**UC4 Architecture Audit (Post-Merge Cleanup):**
- All UC4 services (Java prs-appraisal-service, customer-identity-service, .NET dotnet-prs-appraisal, dotnet-customer-identity) pass domain/infrastructure separation audit
- Domain layer contains ZERO infrastructure imports; persistence adapters properly isolated in `persistence/` or `Infrastructure/` packages
- All 5 gateway interfaces (`RiskIDMQGateway`, `PLUWGateway`, `PLAPRGateway`, `MasterpieceGateway`, `CustomerDBGateway`) cleanly abstracted; stubs in adapter layer
- Event schema naming verified across all 11 UC4 prs.* topics; kafka-setup pre-creates all topics with correct DLQ pattern (`prs.dlq.{route-name}`)
- **Finding:** MongoDB init script was missing `file_processing_db` and `prs_appraisal_db` — fixed
- **Advisory:** .NET gateway stubs located in same `Gateways/` directory as interfaces (acceptable for demo, move to `Infrastructure/Gateways/` before production for consistency with Java pattern)
- Cross-service boundary correctness verified: customer-identity owns ProducerLookupRoute, prs-appraisal owns saga orchestration, no cross-database access
- DLQ patterns verified: `prs.dlq.appraisal-saga-failures` (3 retries), `customer.dlq.producer-lookup` (2 retries)
- **Conclusion:** Architecture is sound; cross-stack parity maintained; no structural rework required

**Three Decisions Merged to decisions.md (2026-05-29):**

**Decision #36:** UC4 Architecture Sweep — Clean Integration
- Documents audit findings, fixes, and architectural validations
- Confirms gateway abstraction pattern works correctly
- Notes .NET stub relocation as post-demo hardening (non-blocking)

**Decision #37:** Demo Reset API in platform-integration-service
- Specifies location: port 8084, four endpoints (health, clear, seed, reset)
- Rationale: Integration service is natural hub; already has MongoDB and HTTP; no new infrastructure
- DemoResetService intentionally in `demo` subpackage (isolated from domain), extractable to future devtools service
- Handles cross-database operations (`prs_appraisal_db`, `dotnet_prs_appraisal_db`) directly via MongoClient

**Decision #38:** Frontend Demo Control Panel — Backend API Contract
- Defines API contract for `/api/demo/reset`, `/api/demo/seed`, `/api/demo/clear`
- Expected response shape for reset: `{success, message, durationMs, steps: [{step, status, message}]}`
- `/api/demo/health` implemented entirely in Next.js (fanout to 21 endpoints)
- All three routes include mock fallbacks for early demoability
- `DEMO_API_URL` env var for runtime backend target configuration

**Udi Dahan Principle Application:**
UC4 gateway pattern again validates the abstract layer principle — 5 unknown external schemas isolated behind interfaces. When real schemas arrive (IBM MQ, @Work, PLAPR), only adapter implementations change; saga logic and observability remain stable. This is Udi Dahan's "don't talk to external systems directly" principle applied to integration patterns.

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

### 2026-05-29 — UC4 Post-Integration Architectural Sweep & Lucid Chart Reference

**Sweep Results (All Pass):**
- Abstract layer separation: ZERO infrastructure imports in domain layers across both stacks
- Event schema naming: All 11 prs.* topics and 30 .NET Contracts events follow conventions
- Kafka topic organization: `prs.*` domain prefix correct, DLQ patterns consistent
- Gateway pattern enforcement: All 5 gateway interfaces in domain, stubs in adapter layer
- Cross-service boundaries: No service reaches into another's database; all inter-service via Kafka
- DLQ patterns: Retry with exponential backoff + DLQ for all saga routes

**Cleanup Applied:**
 - MongoDB init script corrected: pre-creates `file_processing_db` and `prs_appraisal_db`
 - .NET gateway stubs acceptable in current demo location; relocation to `Infrastructure/` deferred to production
 - Cross-database operations for demo reset centralized in platform-integration-service

### 2026-05-31: EDA Compliance Review — All 6 Violations Resolved

**Status:** ✅ Complete. Both Java and .NET UC4 stacks fully EDA-compliant.

**Violations Fixed This Session:**
- **C3 (DocumentRetrievalSaga AtWork Path):** Converted from sync infrastructure call to async pub/sub. Saga publishes `Uc4AppraisalDocumentRetrievalRequestedEvent` → new `AtWorkDocumentRetrievalHandler` → `Uc4AtWorkDocumentRetrievedEvent` reply. Saga waits for reply using same `TryComplete` pattern as DocumentListSaga.
- **I1 (MainframeListAggregatorSaga Startup):** Removed command-bridge ceremony. Saga now starts directly from `Uc4AppraisalDocumentListRequestedEvent` instead of being commanded via `StartMainframeListAggregationCommand` from a handler that had no domain logic.

**All 6 Violations Status:**
| Violation | Type | Finding | Resolution | Status |
|-----------|------|---------|------------|--------|
| C1 | Command | DocumentListSaga AtWork path sync call | Event-driven AtWork handler | ✅ Prior |
| C2 | Command | Mainframe sub-saga not event-driven | MainframeListAggregatorSaga started by event | ✅ Prior |
| C3 | Command | DocumentRetrievalSaga AtWork path sync call | Event-driven AtWorkDocumentRetrievalHandler | ✅ This session |
| I1 | Initialization | MainframeListAggregatorSaga started by command | Direct event startup | ✅ This session |
| I2 | Infrastructure | Infrastructure imports in domain layer | Gateways isolated in adapter layer | ✅ Prior |
| I3 | Messaging | Missing completion events | DocumentRetrievedEvent added | ✅ Prior |

**Key Conventions Established:**
1. **Saga-to-Infrastructure Rule:** Any saga path that calls an external system must route via pub/sub: publish event → create handler → handler calls infrastructure → publishes reply event → saga handles reply. No direct infrastructure calls from saga handlers.
2. **Command-Bridge Elimination:** If a handler's only logic is "subscribe to event X, send command Y to self", collapse to "saga started by event X directly". Events flow to all interested subscribers (saga routing); commands represent imperative intent at entry points only.

**Verification:** 20/20 tests pass, 0 errors. Commit 96914d6.

**Decision Recorded:** `.squad/decisions.md` — UC4 EDA Compliance — C3 AtWork Async Retrieval + I1 Saga Event Subscription

---

## Learnings Summary (2026-05-31)

### Architecture Principles Solidified

1. **DDD + Abstract Layer:** Domain layer must never import infrastructure technology (Kafka, MongoDB, Spring, NServiceBus). All infrastructure is isolated in adapter layers. Gateway pattern ensures domain logic is portable and testable.

2. **EDA Discipline (Udi Dahan):** 
   - Commands for entry points only (user-facing API → first domain service)
   - Never between domain services; use pub/sub events
   - Saga orchestrators publish events and wait for completion events; they never command known participants
   - Saga-to-infrastructure: all external calls route via pub/sub (event → handler → reply event)

3. **Cross-Stack Parity:** Both Java (Camel) and .NET (NServiceBus) implement UC1 and UC4 identically — same saga patterns, same gateway abstraction, same EDA observability. Frontend can switch backends at runtime.

4. **Observability Integration:** EDA_FLOW structured logging unified across both stacks. Real-time ops sequence diagrams render from Loki events with fallback to static topology diagrams.

### UC4 Architectural Outcomes

- **Scope Clarified:** POC targets GetAppraisalList (scatter-gather) and GetAppraisalDocument (content-based routing) only. No UpdateStatus saga in scope.
- **Gateway Abstraction:** 5 gateways (RiskIDMQ, @Work SQL, DEIPDE07 MQ, RiskID WCF, PLUW/PLAPR/Masterpiece) isolated from saga logic; stubs replace easily when schemas are known.
- **EDA Compliance:** All 6 violations (C1-C3, I1-I3) resolved. Both stacks fully compliant.

### Team Conventions Established

- Gateway interfaces in domain layer; implementations in adapter layer
- Saga paths calling external systems: event → handler → reply event (no direct calls)
- Command-bridge elimination: if handler only forwards to self, collapse to direct event-to-saga routing
- `.squad/decisions/inbox/` drop-box pattern for team alignment
- Demo gap markers (`⚠️ STUBBED`, `⚠️ DEMO GAP`) for visibility
- Verification standard: build + tests + container rebuild + runtime check before done
- Fixed `observability/mongo-init.js` — added `file_processing_db` and `prs_appraisal_db` (were auto-created but not in init script)

**Advisory (Non-Blocking):**
- .NET `dotnet-prs-appraisal` gateway stubs collocated with interfaces in `Gateways/` — recommend moving to `Infrastructure/Gateways/` before production

**Deliverable Created:**
- `.docs/architecture-for-lucid-chart.md` — comprehensive architecture reference for Lucid Chart AI prompt generation
- Lists all 35+ running Docker Compose components with stack attribution (Java/NET/Shared)
- Includes event flows for UC1, UC3, UC4; Kafka topic catalog; MongoDB databases; service boundaries; communication matrix
- Decision: `.squad/decisions/inbox/architect-uc4-sweep.md`

**Key File Paths:**
- `.docs/architecture-for-lucid-chart.md` — Lucid Chart AI prompt reference
- `observability/mongo-init.js` — MongoDB database initialization (8 databases)
- `docker-compose.yml` — 35+ services, full platform topology

### 2026-05-31T19:35:23-04:00 — UC4 EDA Compliance Review & Scatter-Gather Validation

**EDA Compliance Review Finding:** UC4 `DocumentListSaga` scatter-gather architecture is incomplete. The AtWork path is synchronous inline (temporal coupling) while mainframe path is correctly event-driven. Requirements define published-event fan-out pattern (§9.2, §10) — this was NOT implemented. Event contracts exist (`Uc4AppraisalDocumentListRequestedEvent`, `Uc4AtWorkDocumentListCompletedEvent`) but were dead code.

**Critical Violations Documented (3 items):**
- C1: Scatter-gather event not published; AtWork called synchronously inline
- C2: Temporal coupling; AtWork blocks saga handler
- C3: Same pattern in DocumentRetrievalSaga

**Important Issues Documented (3 items):**
- I1: Command used where event should be (`StartMainframeListAggregationCommand`)
- I2: Infrastructure reference in domain/saga layer (`AtWorkFixture` in sagas)
- I3: Saga does not wait for AtWork completion event

**Minor Issues Documented (3 items):**
- M1: Unused event contracts (now dead code, will activate with C1 fix)
- M2: EDA Flow log fakes AtWork async behavior (will auto-resolve with C1)
- M3: Command naming pattern in document retrieval path

**What Is Correct:** Mainframe aggregator saga properly event-driven; ArtemisListReplyListener correctly bridges IBM MQ into events; timeout handling implements graceful degradation; saga correlation via RequestId; structured EDA logging enables observability.

**Priority Matrix:** C1+C2+I1+I2+I3 as single sprint (medium effort, high impact). C3 (small effort) as follow-on. M2 (trivial) auto-resolves.

**Key Principle Reinforced:** In NServiceBus scatter-gather, coordinator saga must ONLY publish event and wait for completion events. Never call services inline or send direct commands to known participants. This is Udi Dahan separation: saga doesn't know who will respond, only that responses will arrive.

**Validation Completed (By DotNet Agent):** Scatter-gather implementation committed (7d86cb3) — all 3 critical violations fixed in single refactor pass. Event published, handlers subscribe independently, saga coordinates both AtWork and Mainframe completions.

**Decision Merged:** `architect-eda-compliance-uc4` added to `.squad/decisions/decisions.md` for team reference and ongoing governance.

