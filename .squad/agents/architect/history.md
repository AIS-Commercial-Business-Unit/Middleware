# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel, Kafka, MongoDB, Grafana, Azure (AKS, Blob Storage, Key Vault, App Configuration, APIM, App Insights, Azure Monitor, Azure SignalR Service, Entra ID Managed Identities), Docker, Rancher Desktop, React/Next.js, Java (backend)
- **Architecture:** DDD, SOA (event-driven pub/sub), abstract layer for stack portability — domain layer must not know about infrastructure technology
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Summary of Key Architecture Principles (Through 2026-05-31)

1. **DDD + Abstract Layer:** Domain layer must never import infrastructure technology (Kafka, MongoDB, Spring, NServiceBus). All infrastructure is isolated in adapter layers. Gateway pattern ensures domain logic is portable and testable.

2. **EDA Discipline (Udi Dahan):**
   - Commands for entry points only (user-facing API → first domain service)
   - Never between domain services; use pub/sub events
   - Saga orchestrators publish events and wait for completion events; they never command known participants
   - Saga-to-infrastructure: all external calls route via pub/sub (event → handler → reply event)

3. **Cross-Stack Parity:** Both Java (Camel) and .NET (NServiceBus) implement UC1 and UC4 identically — same saga patterns, same gateway abstraction, same EDA observability. Frontend can switch backends at runtime.

4. **Observability Integration:** EDA_FLOW structured logging unified across both stacks. Real-time ops sequence diagrams render from Loki events with fallback to static topology diagrams. Recent work: handler-invocation logging for fan-out visibility.

5. **UC4 Architectural Outcomes:**
   - Scope: POC targets GetAppraisalList (scatter-gather) and GetAppraisalDocument (content-based routing) only
   - All 6 EDA violations resolved; both stacks fully compliant
   - Gateway Abstraction: 5 gateways isolated from saga logic; stubs replace easily when schemas known

## Recent Learnings

### 2026-05-31 — EDA Handler Invocation Logging & Cross-Stack Observability

- **Handler-level logging pattern:** `.NET dotnet-prs-appraisal` now emits `EDA_Direction = "handled"` at `IInvokeHandlerContext` stage for each subscriber invocation. Enables ops flow tracer to render one arrow per actual subscriber for fan-out events.
- **Participant registry pattern:** `AppraisalParticipantMap.HandlerToParticipant` maps handler classes to readable names; frontend UI displays subscriber identity via `EDA_Handler` field in hover tooltips.
- **Frontend integration:** UC4 saga panel now derives directly from Loki flow events when policy saga endpoint returns null. `isUc4Flow()` detects UC4 traffic; `Uc4SagaPanel` renders scatter-gather progress with subscriber fan-out bracketing.
- **Key contract:** Backend handler logs include `EDA_Handler` at top level or in `Properties` object for frontend Loki parser. Dedup logic preserves `handled` entries (not collapsed into consumed entry).
- **Verification:** Build passes 20/20 tests; TypeScript clean; lint passed. End-to-end: handler logging → Loki → frontend flow API → live diagram with subscriber topology.

### 2026-05-31 — EDA Compliance Review & All Violations Resolved

- **Status:** ✅ Complete. Both Java and .NET UC4 stacks fully EDA-compliant.
- **Violations Fixed:** C3 (DocumentRetrievalSaga AtWork async), I1 (MainframeListAggregatorSaga event startup)
- **Key Pattern Reinforced:** Saga-to-infrastructure rule: any saga path calling external system must route via pub/sub. No direct infrastructure calls from saga handlers.

### Earlier Sessions (Archived)

Full learning history from 2026-05-25 through 2026-05-29 archived in `history-archive-2026-05-31.md`. Key themes:
- UC4 architecture sweep validated all gateway abstractions and cross-service boundaries
- Udi Dahan EDA principles established as team authority
- MongoDB init script corrected for demo databases
- `.docs/architecture-for-lucid-chart.md` reference created for topology visualization

---

**Searchable Conventions:** Gateway interfaces in domain layer; saga-to-infrastructure pub/sub routing; no infrastructure imports in domain; handler-invocation logging for fan-out observability; `.squad/decisions/inbox/` drop-box pattern for team alignment.

