# Squad Decisions

## Active Decisions
### 26. Frontend Hover Tooltips for Ops Sequence Diagram (2026-05-27)
- Add hover tooltips to sequence diagram arrows with event metadata
- Extend `FlowEvent` with typed `details` object: `topic`, `direction`, `stack`, `timestamp`, `description`
- Render HTML overlay anchored to SVG mouse coordinates, not inside SVG
- Derive descriptions from frontend-owned `EVENT_DESCRIPTIONS` map, not Loki payload bodies
- Preserve static-tooltip fallback for `UC1_STEPS` before live logs arrive
- **Verification:** TypeScript clean, build passed, platform-ui restarted, tooltips functional

### 27. Platform-UI Startup Decoupling from Backend Services (2026-05-27)
- `platform-ui` no longer hard-depends on Java or .NET backend service health in `docker-compose.yml`
- One-shot init containers that gate application startup must fail non-zero when provisioning fails
- SQL Server init for `middleware_nsb` is split into two steps: create the database first, then connect to that database to seed `dbo.SubscriptionRouting`
- Keeps frontend available during partial backend outages; hard `depends_on` made UI disappear from `docker ps` when optional backends were warming up
- Real blocker was `sqlserver-init` falsely succeeding while `middleware_nsb` did not exist, cascading into .NET crashes and `dotnet-file-processing` health hangs
- Dependency failures now visible at correct root cause; SQL provisioning failures surfaced early instead of masked by restart loops

### 28. UC4 Gateway Pattern & Saga Structure (2026-05-28)
- All UC4 external integration points MUST use the gateway pattern with domain-layer interfaces and swappable adapter implementations
- Five gateways: RiskIDMQGateway (IBM MQ inbound), PLUWGateway (@Work updates), PLAPRGateway (PLAPR database), MasterpieceGateway (Transaction 90), CustomerDBGateway (producer cross-reference)
- Saga structure uses orchestrator pattern (not choreography) because the workflow is inherently sequential with branching: `AppraisalReceivedSaga` (outer coordinator), `StatusCode6UWSaga` (parallel calls + join), `StatusCode15CompletedSaga` (sequential), `GenericStatusUpdateSaga` (simple pass-through)
- Gateway abstraction ensures saga logic is stable regardless of when/how gaps are resolved
- Orchestrator pattern is appropriate here because the workflow is coordinated, not autonomous reactions, and gateways are adapters within the service boundary, not autonomous services
- This does NOT violate Udi Dahan's pub/sub principle — gateways are infrastructure, not domain services
- **Impact:** Backend must implement gateway interfaces in domain layer; adapter implementations in infrastructure layer. DevOps: gateway stubs need docker-compose entries for demo. All: demo gap document (`.docs/demo-gaps-uc4.md`) is the reference.

### 29. UC4 prs-appraisal-service architecture patterns (2026-05-28)
- **Saga Timeouts:** Implemented `SagaTimeoutRoute` as a `timer:saga-timeout-watchdog?period=60000` Camel timer route (not message-scheduled callbacks). It scans MongoDB for sagas where `timeoutAt < now()` and status is not terminal. Uses MongoDB `findAndModify` with `nin(status, [Completed, Failed, TimedOut])` CAS to prevent double-processing. ProducerTemplate injected as singleton per Decision #13.
- **Parallel Join Pattern:** StatusCode=6 UW Determination requires two parallel operations (PLUW appraisal creation + UW assignment determination) with a synchronization point. Two separate Kafka topics are consumed by two separate routes, both update MongoDB via `findAndModify` setting boolean flags. The route that sets the second flag proceeds to the join. MongoDB `findAndModify` with `ne(status, UpdatingDownstream)` CAS ensures exactly one route proceeds — consistent with atomic join condition (Decision #3).
- **HTTP Endpoint as RiskIDMQGateway Stub:** `AppraisalController.POST /api/appraisal/status-update` acts as the demo stub for `RiskIDMQGateway`. Production MQ integration will replace the controller body without changing the saga. Demo entry point is `http://localhost:8090/api/appraisal/status-update`.
- **Impact:** Saga timeout pattern is reusable for any long-running Camel saga. Watchdog delay tunable via `appraisal.saga.timeout-minutes`. Parallel join extends the UC1 IssuanceSagaRoute pattern to UC4. Both UC1 and UC4 use `findAndModify` with status-based CAS for join synchronization.

### 30. DevOps — Renewal drop-zone bind mounts (2026-05-28)
- Use repo-local bind mounts under `.docker-data/renewals` for file-processing drop-zones in `docker-compose.yml` instead of named volumes
- Java service writes through `./.docker-data/renewals/java:/app/data`, creating `renewals/inbound`, `processed`, and `error` directories itself
- .NET service needs explicit subdirectory bind mounts because a fresh non-root bind mount at `/app/data` caused permission failures when creating `/app/data/renewals`
- **Why:** Dockerfile-created directories don't work when a named volume is mounted over the mount path; the mount hides image-layer directories
- **Impact:** Java batch generation succeeds locally and generated CSV is visible under `.docker-data/renewals/java/renewals/inbound`. .NET drop-zone directories are created as host bind mounts. `.docker-data/` remains ignored by git.

### 31. UC4 .NET stack gateway pattern (2026-05-28)
- All UC4 appraisal service external integration points exposed through named interfaces (`IRiskIDMQGateway`, `IPLUWGateway`, `IPLAPRGateway`, `IMasterpieceGateway`, `ICustomerDBGateway`) with stub implementations that log `⚠️ STUBBED:` warnings with `REPLACE_ME_*` constants
- Gateway instances wired to the static `AppraisalRuntime` class at `Program.cs` startup, consistent with the `CustomerIdentityRuntime` pattern established in UC1

### 32. New Maven modules require Dockerfile POM sync (2026-05-29)
- When a new module is added to `java/pom.xml`, ALL Dockerfiles under `java/` must receive a corresponding `COPY {module}/pom.xml {module}/` line in the POM-copy block
- Missing module causes hard build failure: `[ERROR] Child module /workspace/{module} does not exist`
- Whoever adds a module to `java/pom.xml` is responsible for patching all Dockerfiles in the same commit
- DevOps will catch missing POM-copy lines in build review

### 33. MongoConfig Required for Every Service Using OffsetDateTime in MongoDB (2026-05-29)
- Spring Data MongoDB 4.x does not natively support `java.time.OffsetDateTime` without custom codec
- Missing codec causes `CodecConfigurationException` and DLQ failures
- Every Java service that uses Spring Data MongoDB AND has `OffsetDateTime` fields in domain/persistence classes MUST include `MongoConfig.java`
- Copy canonical template from `policy-issuance-service/config/MongoConfig.java`; converters: `OffsetDateTimeToDateConverter` (OffsetDateTime → BSON Date UTC) and `DateToOffsetDateTimeConverter` (BSON Date → OffsetDateTime UTC)
- Checklist: MongoDB dependency? OffsetDateTime fields? If both: add `MongoConfig.java` before first PR

### 34. Use `.nin()` for Multi-Value Exclusion in MongoDB Criteria (2026-05-29)
- Chained `.ne()` calls on same field throw `InvalidMongoDbApiUsageException`
- When excluding multiple values from same field, use `.nin(val1, val2, ...)` NOT chained `.ne()` calls
- Example: `Criteria.where("_id").is(id).and("status").nin("A", "B")` ✅ not `and("status").ne("A").and("status").ne("B")` ❌
- Applies to `findAndModify`, `find`, and `update` queries
- **Rationale:** Demo requires ALL integration points to be visible without real systems. Gateway stubs must be observable. `REPLACE_ME_*` constants make demo gaps searchable. Static runtime pattern avoids NServiceBus DI container complexity.
- **Impact:** `dotnet-prs-appraisal` builds and runs standalone with all stubs. `dotnet-customer-identity` extended (not replaced) — ProducerLookupHandler added. `Middleware.sln` updated with new project. `docker-compose.yml` updated — port 8189.

### 32. UC4 Demo Shell — Demo Gap Visibility Pattern (2026-05-28)
- When building a demo page for a use case where the backend service is not yet implemented, frontend API proxy routes should:
  1. Try to call the real backend service
  2. On any failure (connection refused, timeout, 404, 503), return typed mock/stub data with `isMockData: true` in the response body
  3. The UI should display the mock data flag prominently — banner at the top, `⚠️ DEMO GAP` badges on every mock field, expandable requirements gap panel listing all open questions
- Applied to: `platform-ui/src/app/uc4/page.tsx` (UC4 Appraisal Documents page), `platform-ui/src/app/api/riskid/status-update/route.ts` (returns stub saga on integration service failure), `platform-ui/src/app/api/riskid/sagas/route.ts` (returns seeded mock sagas on appraisal service failure)
- **Rationale:** Appraisal Service and Integration Service appraisal endpoints not yet implemented. Frontend page needs to be demoable now to show architecture pattern and requirements gaps. Making demo gaps highly visible drives the questions that need to be answered by the PRS developer.

### 33. Integration — Renewal Volume Bootstrap (2026-05-28)
- Keep the existing `renewal-data:/app/data/renewals` volume mount, but bootstrap the mounted directory at container start
- Container entrypoint creates `inbound`, `processed`, and `error`, fixes ownership/permissions on the mounted volume root, then launches the Java process as `appuser`
- **Why:** Dockerfile-created directories do not work when a named volume is mounted over the mount path (the mount hides image-layer directories)
- **Impact:** Preserves the non-root runtime decision for Java services. Avoids docker-compose volume mount changes. Makes startup fail fast if drop-zone directories are missing or not writable.

### 34. UC4 RiskIDMQGateway — Topic Naming and Integration Seam (2026-05-28)
- **Topic Naming:** PRS domain topics use `prs.*` prefix — `prs.events.appraisal-received`, `prs.dlq.riskid-gateway`. Consistent with existing single-word domain naming convention (policy, compliance, customer, billing, file, integration).
- **IBM MQ Entry Point Seam:** Is `direct:riskid-kafka-publish`. The HTTP controller is demo scaffolding only. The Camel route entry point is the explicit cut-point where the real IBM MQ JMS consumer will plug in at production time. No other code changes needed when switching from HTTP to MQ.
- **Correlation Key:** `appraisalId` is the UC4 correlation key (analogous to `issuanceId` in UC1). Stored as `correlationId` on the exchange so `EDAFlowProcessor.resolveIssuanceId()` handles it via the existing fallback path without changes to the observability layer.
- **Canonical Published Event:** `AppraisalReceivedEvent` is the canonical published event. Fields are explicitly marked `// ⚠️ DEMO GAP` until the PRS integration team provides the real IBM MQ wire schema.
- **Rationale:** IBM MQ format is unknown — keeping the demo gap markers explicit and traceable forces the real confirmation conversation with the PRS developer before go-live. The seam approach means the appraisal saga logic can be built and tested now, decoupled from the real MQ plumbing.
- **Impact:** `kafka-setup` pre-creates `prs.events.appraisal-received` and `prs.dlq.riskid-gateway` at startup. Appraisal domain service subscribes to `prs.events.appraisal-received` with its own consumer group.

### 35. QA — UC4 Demo Gap Documentation Standard (2026-05-28)
- **For any BizTalk replacement feature demo, QA will produce two distinct sections:**
  1. Architecture test scenarios — verifiable against the running docker-compose stack. Test that the *patterns* work: saga state management, content-based routing, parallel join, EDA_FLOW observability, DLQ handling, retry logic.
  2. Demo gap scenarios — explicit documentation of what cannot be verified without real system data. Each gap gets a risk level (HIGH/MEDIUM/LOW) and a specific question for the domain expert.
- **Gateway stubs must include `⚠️ STUBBED` in log output** so that during demo, the presenter can point at the log and show the audience exactly where the real integration boundary is
- **Rationale:** Stakeholders include PRS domain experts who know the actual message formats and business rules. Making demo gaps highly visible (rather than hiding them) builds trust: "we know what we don't know."
- **Impact:** All future UC demos should follow this two-section structure. `⚠️ STUBBED` log markers become a team-wide convention. Prep session agendas should include a "gap validation" block.

### 36. UC4 Architecture Sweep — Clean Integration (2026-05-29)
- All UC4 services (Java prs-appraisal-service, customer-identity-service, .NET dotnet-prs-appraisal, dotnet-customer-identity) pass domain/infrastructure separation audit
- Domain layer contains ZERO infrastructure imports; persistence adapters properly isolated in `persistence/` or `Infrastructure/` packages
- All 5 gateway interfaces (`RiskIDMQGateway`, `PLUWGateway`, `PLAPRGateway`, `MasterpieceGateway`, `CustomerDBGateway`) cleanly abstracted; stubs in adapter layer
- Event schema naming verified across all 11 UC4 prs.* topics; kafka-setup pre-creates all topics with correct DLQ pattern
- MongoDB init script fixed to pre-create `file_processing_db` and `prs_appraisal_db`
- Gateway stub relocation in .NET (from `Gateways/` to `Infrastructure/Gateways/`) deferred to production hardening (not a demo blocker)
- **Impact:** Architecture is sound; cross-service boundary correctness verified; no structural rework required

### 37. Demo Reset API in platform-integration-service (2026-05-29)
- UC4 demo reset orchestration (`GET /api/demo/health`, `POST /api/demo/clear`, `POST /api/demo/seed`, `POST /api/demo/reset`) lives in **platform-integration-service** (port 8084)
- Rationale: Integration service already has MongoDB and HTTP dependencies; no new infrastructure added. Natural hub for cross-system demo tooling. Single `MongoClient` handles cross-database operations (`prs_appraisal_db`, `dotnet_prs_appraisal_db`)
- `DemoResetService` intentionally isolated in `demo` subpackage, extractable to future `platform-devtools-service` without domain changes
- **Impact:** Four endpoints added to platform-integration-service; `demo.health.*` properties added to `application.yml` with Docker-internal defaults

### 38. Frontend Demo Control Panel — Backend API Contract (2026-05-29)
- Platform UI `/demo-control` page proxies three mutation endpoints (`/api/demo/reset`, `/api/demo/seed`, `/api/demo/clear`) to backend via `DEMO_API_URL` environment variable
- Default target: `http://policy-issuance-service:8081` (overridable once Backend finalizes owner service)
- Expected response contract for `/api/demo/reset`: JSON with `success`, `message`, `durationMs`, and `steps` array (each step has `step`, `status` ["ok"|"error"], `message`)
- `GET /api/demo/health` implemented entirely in Next.js layer — fans out to all 21 service health endpoints in parallel; no backend changes required
- All three routes include mock fallbacks (`isMockData: true`) for early demoability before backend implementation
- **Impact:** Frontend demo page ready to ship; backend implementation gates on Decision #37

### 39. Verification Standards for Completion (2026-05-29)
- All UI/frontend work must include browser verification before declaring done
- Build artifacts (pages, components, APIs) must be tested end-to-end in their runtime environment, not just validated that files were created
- **Applies to:** Frontend work (new routes, pages, components), Backend APIs (verify endpoints respond correctly), Containerized service changes (verify health after rebuild)
- **Required before declaring done:** File artifacts created ✅ Build succeeds (if applicable) ✅ Container rebuilt (if applicable) ✅ **Runtime verification in target environment** ✅ Log evidence provided (for backend/integration work)
- This is a quality gate, not optional
- **Rationale:** `/demo-control` page returned 404 after container rebuild, caught by user not team. Verification gaps undermine demo readiness and waste session time.

### 40. BusyBox wget Health Checks Must Use 127.0.0.1, Not localhost (2026-05-29)
- In BusyBox-based Docker containers (Alpine, mongo-express, etc.), `localhost` resolves to `::1` (IPv6 loopback) by default; if service binds to `0.0.0.0` (IPv4 only), `wget -qO- http://localhost:<port>/` fails with "Connection refused" even though the service is fully operational
- **All Docker health checks using BusyBox wget MUST use `127.0.0.1` (explicit IPv4) instead of `localhost`**
- Applies to all BusyBox-based images in docker-compose.yml; images using full glibc (Debian/Ubuntu-based) also respect `127.0.0.1` so this change is safe everywhere
- Applied to `mongo-express` (fixed FailingStreak 644 → healthy)
- Apply to any new health checks added to docker-compose.yml; review existing health checks when adding new BusyBox-based services


### 49. 20260607125934: EDA pattern directive — sagas start from events, never commands
**By:** Steven Suing (via Copilot)
**What:** When a saga or handler needs to be triggered by a workflow step, publish an event and let the saga/handler subscribe to it. Never send a direct command to a saga when an event already exists that semantically represents the same intent. The canonical example is AppraisalDocumentRetrievalRequestedEvent — both AtWorkDocumentRetrievalHandler and MainframeDocumentAggregatorSaga must subscribe to it, not be sent commands. Collapse any 'subscribe to event then send command to self' indirection into a direct event subscription on the saga.
**Why:** User request — reinforcing event-driven architecture, avoids point-to-point command coupling between sagas

### 50. (Merged from dotnet-accumulator-refactor.md)
Decision: Move mainframe MQ accumulation off NServiceBus saga rows (2026-06-01)

Context:
- `MainframeListAggregatorSaga` and `MainframeDocumentAggregatorSaga` were storing concurrent MQ part/chunk updates directly on saga rows.
- NServiceBus SQL persistence uses optimistic concurrency on saga row versions, so simultaneous part events retried and amplified duplicate upstream MQ work.

Decision:
- Persist mainframe list parts in side tables `mf_list_headers` and `mf_list_parts`, keyed by request id and sequence number.
- Persist mainframe document chunks in side tables `mf_document_headers` and `mf_document_chunks`, and let the first final-chunk handler that flips `CompletedAt` publish the completion event.
- Keep sagas responsible for request kickoff, timeout ownership, and publishing the existing downstream completion events, but remove part/chunk accumulation state from saga data.
- Create the side tables at service startup with `AccumulatorRepository.EnsureCreatedAsync()` using the existing `ConnectionStrings:NServiceBus` SQL connection.

Consequences:
- Concurrent MQ replies no longer hot-spot the same saga row, so list/document accumulation can complete without row-version retry storms.
- Timeout behavior stays in the sagas; list timeout can still emit partial results by reading the accumulator tables, while document timeout preserves the empty fallback.
- The Kafka bridge contract remains stable because downstream events (`MainframeDocumentListCompletedEvent`, `AppraisalDocumentRetrievedEvent`) are unchanged.

### 51. (Merged from dotnet-callbacks-to-polling.md)
Decision: Replace NServiceBus Callbacks with MongoDB Polling (2026-06-01)

### 52. 2026-06-07: MainframeDocumentAggregatorSaga now starts from AppraisalDocumentRetrievalRequestedEvent
**By:** DotNet (requested by Steven Suing)
**What:** Removed StartMainframeDocumentAggregationCommand. DocumentRetrievalSaga now always publishes AppraisalDocumentRetrievalRequestedEvent (with SourceSystem field) for both AtWork and Mainframe paths. MainframeDocumentAggregatorSaga subscribes to this event (skipping AtWork requests). AtWorkDocumentRetrievalHandler likewise skips Mainframe requests.
**Why:** Enforces the established EDA pattern: handlers/sagas must start from published events, not point-to-point commands. Mirrors DocumentListSaga pattern (AppraisalDocumentListRequestedEvent fans out to both AtWork and Mainframe handlers). Eliminates command coupling between DocumentRetrievalSaga and MainframeDocumentAggregatorSaga.
**Impact:** StartMainframeDocumentAggregationCommand deleted. SourceSystem added to AppraisalDocumentRetrievalRequestedEvent. All existing tests updated.

### 53. (Merged from ui-sequence-diagram-lifeline-convention.md)
# Decision: EDA Sequence Diagram Lifeline Convention

**Author:** Copilot (UI/observability)  
**Date:** 2025-08-01  
**Status:** Proposed

## Context

The UC4 EDA flow tracer renders a sequence diagram from structured Loki log entries.
Early versions used endpoint-level lifelines (`PrsAppraisal`) instead of handler-level
lifelines, causing self-arrows (e.g. `PrsAppraisal → PrsAppraisal`) and NServiceBus
routing artefacts (`broadcast`, `AllSubscribers`) to appear as participants.

## Decision

### 1. Lifeline granularity — handlers and sagas, not endpoints

Lifelines must represent the **handler or saga** that performs work, not the NServiceBus
endpoint that hosts it.  The hosting endpoint is shown as a small `«qualifier»` above
the lifeline label.

Format inside the SVG lifeline box:

```
┌──────────────────────┐
│  «prs-appraisal»     │  ← italic, 8 px, 60 % opacity
│  Document            │  ← bold, 10 px, white, wrapped
│  List Saga           │
└──────────────────────┘
```

### 2. Participant ID contract

The `id` field in the UI `PARTICIPANTS` constant **must match exactly** (case-sensitive)
the values emitted into `EDA_From`, `EDA_To`, and `EDA_Handler` by `EDAFlowBehavior.cs`.

Canonical UC4 participant IDs:

| ID                             | Endpoint qualifier | Display label            |
|--------------------------------|--------------------|--------------------------|
| `user`                         | *(actor)*          | User (stick figure)      |
| `AppraisalDocumentsController` | `prs-appraisal`    | Appraisal Documents Ctrl |
| `DocumentListSaga`             | `prs-appraisal`    | Document List Saga       |
| `DocumentRetrievalSaga`        | `prs-appraisal`    | Document Retrieval Saga  |
| `MainframeListAggregator`      | `prs-appraisal`    | Mainframe List Aggregator|
| `MainframeDocumentAggregator`  | `prs-appraisal`    | Mainframe Doc Aggregator |
| `AtWorkDocumentListHandler`    | `prs-appraisal`    | AtWork Doc List Handler  |
| `AtWorkDocumentRetrievalHandler` | `prs-appraisal` | AtWork Doc Retrieval Hdlr|
| `AtWork`                       | `atwork`           | AtWork SQL               |
| `Mainframe`                    | `mainframe`        | IBM MQ (Mainframe)       |

### 3. User actor

The initiating lifeline for any flow triggered by a human is `user` with `isActor: true`.
It renders as a UML stick figure.  In the live diagram a synthetic
`user → AppraisalDocumentsController: HTTP GET /api/documents` step is prepended
automatically when `AppraisalDocumentsController` appears in the event set.

### 4. Suppress routing artefacts everywhere

`broadcast` and `AllSubscribers` must **never** appear as lifelines or step endpoints.
They are NServiceBus routing implementation details, not EDA participants.

Suppression happens at two levels (defence in depth):
1. **`EDAFlowBehavior.cs`** — the outgoing behavior only logs messages with a
   *known point-to-point target*; fan-out events are shown through each subscriber's
   `handled` entry instead.
2. **`platform-ui` route.ts + page.tsx** — a `SUPPRESSED_IDS` set removes any event
   or participant whose `from`/`to` matches `broadcast` or `allsubscribers`
   (case-insensitive).

### 5. Outgoing behavior uses AsyncLocal handler context

`AppraisalEDAFlowOutgoingBehavior` attributes outgoing messages to the **currently
executing handler** (via `AsyncLocal<string?> AppraisalCurrentHandlerContext.HandlerTypeName`)
rather than the endpoint name.  This ensures arrows like
`DocumentListSaga → AtWorkDocumentListHandler` are accurate even when both reside in the
same endpoint.

### 6. Long labels wrap with `\n`

Labels in `PARTICIPANTS` use `\n` to force line breaks.  The SVG renderer splits on `\n`
and stacks `<text>` elements.  Keep labels ≤ 3 lines to fit the fixed box height.

## Consequences

- Adding a new handler/saga to `dotnet-prs-appraisal` requires a matching entry in
  both `HandlerToParticipant` (behavior) and `PARTICIPANTS` (UI).
- The participant ID must not change once used in production logs (it is burned into Loki).
  If a rename is required, add the old ID as an alias in the UI mapping.


## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction


# Decision: UC4 POC Scope Correction — Appraisal Query Services Only

**Date:** 2026-05-30  
**Author:** Architect (Steven Suing)  
**Source:** Developer meeting 2026-05-29 with MJ (Mruthunjaya Jodukapli, BizTalk developer) and Siva Narapareddy (client lead)  
**Status:** Confirmed — all UC4 documentation updated

---

## Decision

The UC4 POC scope is **GetAppraisalList** and **GetAppraisalDocument** only.

All prior documentation describing the UpdateStatus saga (StatusCode 6/15, PLUW, PLAPR, Masterpiece, underwriter determination) as the demo target was incorrect. That documentation has been replaced.

---

## What Was Corrected

### Previous (incorrect) scope
- UC4 demo described as: UpdateStatus saga from RiskID — complex workflow with parallel UW determination, multi-database updates, PLUW creation, Masterpiece Transaction 90
- Documentation included: AppraisalUnderwriterAssigned, AppraisalCompleted events; UpdateStatusSagaData aggregate; PLAPR deduplication rules; Masterpiece and PLUW gateway stubs

### Confirmed (correct) scope
- **GetAppraisalList** — scatter-gather fan-out to @Work SQL stored procedure and DEIPDE07 mainframe via IBM MQ simultaneously; merge and deduplicate results; return combined appraisal list with DocumentKeys
- **GetAppraisalDocument** — content-based router: if DocumentKey contains `_RiskID_` → call RiskID WCF service (`GetInspectionPDFBizTalk`); if DocumentKey is numeric-only → call DEIPDE07 mainframe via IBM MQ with multi-message chunk aggregation

---

## Key Facts Confirmed by MJ

1. The BizTalk application has ~19 integrations in the PRS estate. The POC targets 2 of them.
2. GetAppraisalList and GetAppraisalDocument are **independent standalone workflows** — there is no outer orchestration wrapper that dispatches to them.
3. GetAppraisalDetails is **out of scope** for the POC.
4. The UpdateStatus saga is **out of scope** for the POC.
5. The hardest architectural challenge in the POC is the **multi-message aggregation** for GetAppraisalDocument from DEIPDE07: the mainframe sends the PDF as N separate MQ messages, each containing a 64-byte chunk of base64 content with EBCDIC newlines. All chunks must be collected in order, EBCDIC artifacts stripped, and concatenated into the final base64 PDF string. In BizTalk this was handled by a .NET helper class with no observability.
6. DEIPDE07 MQ timeouts: 1-second initial check, 30-second overall for GetAppraisalList; 30-second overall for GetAppraisalDocument chunk aggregation.
7. MJ will share sample XML payloads via the Teams channel — these are needed to finalize stub implementations.

---

## Architectural Patterns in Scope

| Pattern | Workflow |
|---|---|
| Scatter-Gather with Timeout | GetAppraisalList |
| Content-Based Routing | GetAppraisalDocument |
| Async-to-Sync Bridge | Both MQ paths |
| Multi-Message Aggregation | GetAppraisalDocument (DEIPDE07 path) |
| EBCDIC-to-ASCII Conversion | GetAppraisalDocument (DEIPDE07 path) |

---

## Documentation Updated

All six UC4 documentation files have been rewritten to reflect this scope:

| File | Change |
|---|---|
| `.docs/req/25-appraisal.md` | Ubiquitous language, data model, commands, events, business rules, test artifacts — all updated; UpdateStatus saga removed entirely |
| `.docs/req/uc4-appraisal-updatestatus-demo-plan.md` | Entire file replaced with correct demo plan for GetAppraisalList + GetAppraisalDocument |
| `.docs/req/uc4-internal-demo-prep.md` | Entire file replaced; questions answered, implementation tasks written |
| `.docs/req/uc4-demo-prep-INTERNAL.md` | Entire file replaced; task list rewritten for correct scope |
| `.docs/uc4-demo-script.md` | Entire file replaced; demo script for 7 correct scenarios |
| `.docs/uc4-test-scenarios.md` | Entire file replaced; SC-001 through SC-007 for correct workflows |

---

## Impact on Other Domains

- **Platform.Integration** — no UpdateStatus MQ consumer or PLUW/Masterpiece gateway needed for the POC
- **Customer domain** — ProducerCrossReference lookup is NOT needed for the POC (it was only required for the UpdateStatus saga)
- **Stubs needed** — @Work SQL (list), DEIPDE07 MQ (list + document), RiskID WCF (document only)


# Decision: DEIPDE07 MQ Simulator stub built

**Date:** 2026-05-30  
**Author:** Backend  
**Requested by:** Steven Suing  

## Decision

The `deipde07-mq-simulator` Spring Boot service has been created at `java/stubs/deipde07-mq-simulator/` and wired into the Maven reactor.

## What Was Built

- `pom.xml` — inherits from middleware-platform parent; deps: `spring-boot-starter-web`, `spring-boot-starter-actuator`, `spring-boot-starter-artemis`
- `Dockerfile` — multi-stage build following rsk3x3-compliance-stub pattern; exposes port 9020; includes all existing reactor modules plus new `stubs/deipde07-mq-simulator/pom.xml`
- `.dockerignore` — identical pattern to other stubs
- `Deipde07SimulatorApplication.java` — `@SpringBootApplication` + `@EnableJms`
- `config/JmsConfig.java` — placeholder; Spring Boot Artemis autoconfiguration handles wiring
- `simulator/MqRequestDispatcher.java` — `@JmsListener` on `${mq.request.queue}`; dispatches to `AppraisalListResponder` or `DocumentChunkResponder` based on body parse; catches all exceptions to keep listener alive
- `simulator/AppraisalListResponder.java` — fixture data for POL-001-TEST (3 records), POL-002-TEST (0 records), POL-003-TEST (1 record), POL-TIMEOUT (35s sleep); sends multi-message JMS responses with `SEQUENCE=X OF N` format
- `simulator/DocumentChunkResponder.java` — fixture PDFs for keys `12345678901` (small) and `98765432109876` (large); sends 64-byte chunks with `\r\n` CRLF artifact; last chunk gets `||END-OF-DOCUMENT||` sentinel
- `util/MessageParser.java` — `isAppraisalListRequest`, `isDocumentRequest`, `parsePolicyNumber`, `parseDocumentKey`
- `src/main/resources/application.yml` — Artemis native mode, all queue names and delays configurable via env vars

## Key Design Notes

- Uses `jakarta.jms.*` (not `javax.jms.*`) — required for Spring Boot 3.x
- Structured logging on every dispatch: correlationId, policyNumber/documentKey, messageCount
- All configurable parameters exposed as `@Value` fields with `${ENV_VAR:default}` pattern
- Parent POM already had `stubs/deipde07-mq-simulator` as a reactor module
- JmsTemplate used for all sends (not Session.createProducer) — consistent with Spring idiom


# Decision: Kafka-UI Minimum Memory Configuration

**Date:** 2026-05-29T10:49:51.724-04:00  
**Author:** DevOps  
**Trigger:** Kafka-UI "fetch failed" errors caused by OOM at 93.8% of 128MiB limit

## Decision

Kafdrop (kafka-ui) requires a minimum of `mem_limit: 384m` and `JVM_OPTS: "-Xms32M -Xmx128M"` when serving 30+ Kafka topics with 3 partitions each.

## Rationale

Kafdrop is a Spring Boot + Undertow JVM application. JVM total footprint = heap + metaspace + native threads + NIO buffers. With `Xmx64M`, the observed container RSS was 120 MiB — leaving only 8 MiB headroom in a 128m container. Under topic-fetch load, GC pressure caused "fetch failed" errors at the UI.

Increasing to `Xmx128M` + `mem_limit: 384m` drops steady-state usage to ~58% (224 MiB / 384 MiB), providing ~160 MiB headroom for burst fetches and GC cycles.

## Rule

- `kafka-ui` in `docker-compose.yml`: always `mem_limit: 384m` and `JVM_OPTS: "-Xms32M -Xmx128M"` minimum.
- If the topic count exceeds 50, bump to `mem_limit: 512m` and `Xmx192M`.
- Never set `Xmx` within 64M of `mem_limit` for Spring Boot apps — the JVM footprint overhead is always ~2× the heap size.


# Decision: Resource Limit Hardening for Infra-Critical Containers

**Author:** DevOps  
**Date:** 2026-05-29T11:03:42.465-04:00  
**Triggered by:** Steven Suing — intermittent container instability; PRS Appraisal timeout/recovery pattern

---

## Decision

Apply conservative resource limits and explicit JVM/Go heap bounds to the four infra-critical containers that were running at ≥67% of their memory limits in steady state. The rule is: **steady-state memory usage should not exceed 60% of container limit**, leaving headroom for GC bursts, compaction spikes, and log volume increases.

## Changes Applied

| Container | Old Limit | New Limit | JVM/Go Bound Added |
|-----------|-----------|-----------|---------------------|
| tempo | 512m | 768m | `GOMEMLIMIT=650MiB` |
| kafka | 1g | 1.5g | `KAFKA_HEAP_OPTS=-Xms256m -Xmx768m` |
| zookeeper | 256m | 384m | `ZOOKEEPER_HEAP_SIZE=256` |
| promtail | 128m | 256m | — (Go runtime, self-managed) |

## Rationale

- **Tempo at 97%** was the primary risk. Tempo's block compaction is in-memory; without `GOMEMLIMIT`, the Go GC has no knowledge of the container limit and will not collect aggressively until the OOM killer fires. `GOMEMLIMIT` sets a soft ceiling that causes the GC to run before the hard limit is hit.
- **Kafka without `KAFKA_HEAP_OPTS`** auto-sizes to ~25% of host RAM, which can be several GB. Explicit bounds prevent both over-allocation and prevent the JVM from approaching the container limit.
- **`ZOOKEEPER_HEAP_SIZE`** is the cp-zookeeper-specific env var (integer MB) for controlling heap. It is equivalent to setting both `-Xms` and `-Xmx`.
- **Promtail** tails Docker socket logs from all containers. With 37 containers producing logs, memory grows proportionally with log volume. Doubling the limit is appropriate insurance.

## Rules for Future Containers

1. **JVM containers (Kafka, Kafdrop, Zookeeper, Java services):** Always set explicit `-Xms`/`-Xmx`. Rule of thumb: `Xmx` ≤ 50% of `mem_limit`. Reserve the rest for non-heap (metaspace, OS, NIO buffers, native threads).
2. **Go containers (Tempo, Loki, Prometheus, Grafana):** Set `GOMEMLIMIT` ≈ 85% of `mem_limit` so the GC receives a soft signal before the container OOM kills the process.
3. **Steady-state target:** No container should exceed 60% of its `mem_limit` during normal operation. If `docker stats --no-stream` shows ≥75% on a given container, that service needs a limit increase before the next demo or load run.

## Root Cause of PRS Appraisal "timeout and comeback"

Not a container failure. The perceived instability was Tempo (97%) doing block compaction → GC pressure → otel-collector trace export backup → services spending extra flush time → UI timing out on the first cold-start requests. Resolved entirely by fixing Tempo's memory allocation. No changes needed to prs-appraisal-service or dotnet-prs-appraisal health checks.


# Decision: UC4 New Services Wired into docker-compose + POM + Dockerfiles

**Author:** devops  
**Date:** 2026-05-30  
**Status:** Proposed  

## Decision

Wire `activemq-artemis`, `deipde07-mq-simulator`, and updated `prs-appraisal-service` into the local development stack.

## Changes

### `java/pom.xml`
- Added `<module>stubs/deipde07-mq-simulator</module>` after `stubs/crm19x1-billing-stub`

### All 15 Java Dockerfiles
- Added `COPY stubs/deipde07-mq-simulator/pom.xml stubs/deipde07-mq-simulator/` in POM-copy block of every Java multi-stage Dockerfile
- Required because all Java builds use the Maven reactor; every Dockerfile must declare all sibling module POMs or the reactor resolution fails

### `docker-compose.yml` — New services added

**`activemq-artemis`** (apache/activemq-artemis:2.37.0)
- Ports: 61616 (JMS/OpenWire), 8161 (Web Console), 5672 (AMQP)
- Healthcheck: `curl -sf http://127.0.0.1:8161/console/`
- mem_limit: 384m
- Positioned in EXTERNAL SYSTEM STUBS section

**`deipde07-mq-simulator`** (custom Java build from `stubs/deipde07-mq-simulator/Dockerfile`)
- Port: 9020 (Spring Boot actuator + app)
- Depends on: `activemq-artemis` healthy
- Healthcheck: `wget -qO- http://127.0.0.1:9020/actuator/health`
- mem_limit: 256m
- Environment: broker URL, queue names, simulated delay config

### `docker-compose.yml` — `prs-appraisal-service` updated

- `depends_on` extended with `activemq-artemis: condition: service_healthy` and `deipde07-mq-simulator: condition: service_healthy`
- Environment variables added: `ARTEMIS_BROKER_URL`, `ARTEMIS_USER`, `ARTEMIS_PASSWORD`, `JAVA_TOOL_OPTIONS: "-Xmx256m"`

## Rationale

- UC4 PRS Appraisal flow requires communication with a legacy MQ system (deipde07). The simulator stands in for the real system locally.
- `prs-appraisal-service` must not start before the MQ simulator is healthy, preventing connection errors on startup.
- `JAVA_TOOL_OPTIONS: "-Xmx256m"` = 50% of 512m mem_limit — consistent with team JVM memory rule (Xmx ≤ 50% of container limit).
- `127.0.0.1` in all healthcheck tests per BusyBox IPv6 team decision.


# Decision: IBM MQ Stub Approach for UC4 POC Demo

**Author:** Integration  
**Date:** 2026-05-30  
**Scope:** UC4 GetAppraisalList + GetAppraisalDocument DEIPDE07 mainframe stubs  
**Full design:** `.docs/req/uc4-mq-stub-design.md`

---

## Decision

**Use ActiveMQ Artemis as the JMS broker, plus a custom `deipde07-mq-simulator` Spring Boot service** that impersonates the DEIPDE07 mainframe response patterns.

Reject IBM MQ Developer Docker (`icr.io/ibm-messaging/mq:latest`) for the POC demo environment.

---

## Rationale

**Against IBM MQ Docker:**
- ~1.8 GB image adds significant pull time; problematic for first-run `docker compose up` in demo conditions
- Requires explicit IBM license acceptance at container start — breaks unattended startup
- IBM MQ JMS provider JARs are not on Maven Central; require manual download or a private Nexus mirror we don't have
- No built-in queue browser UI suitable for live demo visibility

**For ActiveMQ Artemis:**
- Built-in web console at port 8161 — demo audience watches queue depths change in real time as requests flow through
- `camel-jms` + `artemis-jms-client` both on Maven Central — zero setup friction
- `Apache.NMS.ActiveMQ` NuGet for .NET — same API surface as IBM MQ .NET client
- ~350 MB image, Apache 2.0 license, zero friction startup
- The DEIPDE07 patterns (correlation ID, multi-message, 64-byte chunks, end-of-message sentinel) are JMS semantics — they work identically on Artemis

**Pattern authenticity is preserved:** The Camel route DSL is identical for Artemis and IBM MQ. The only difference is the JMS connection factory bean. When the real DEIPDE07 connection is available, only the infrastructure config changes — no route logic, no gateway interface, no domain code.

---

## Impact

- Add `activemq-artemis` and `deipde07-mq-simulator` services to `docker-compose.yml`
- Camel services use `camel-jms` + `artemis-jms-client` (not `camel-ibm-mq`)
- .NET services use `Apache.NMS.ActiveMQ` NuGet
- Queue names are fixed: `MQP.REQUESTQUEUE.1` (requests), `MQP.RESPONSEQUEUE.1` (responses) — match intended production naming

---

## Production Migration

When the real IBM MQ endpoint is available:
1. Swap JMS connection factory bean to IBM MQ provider
2. Remove `deipde07-mq-simulator` from compose (or gate behind `profiles: [stub]`)
3. No changes to Camel routes, gateway interfaces, or domain code


# Decision: prs-appraisal-service UC4 Scope Correction

**Author:** Integration (squad agent)  
**Date:** 2026-05-30  
**Requested by:** Steven Suing  
**Status:** Implemented

---

## Decision

`prs-appraisal-service` has been completely rewritten. The previous implementation (UpdateStatus saga with StatusCode 6/15, PLUW, PLAPR, Masterpiece) was wrong scope and has been **removed entirely**. The service now implements the correct UC4 scope.

---

## What Was Removed

- All 5 saga route files (`AppraisalReceivedSagaRoute`, `GenericStatusUpdateSagaRoute`, `SagaTimeoutRoute`, `StatusCode15CompletedSagaRoute`, `StatusCode6UWSagaRoute`)
- MongoDB saga persistence (`AppraisalSagaDocument`, `AppraisalSagaMongoRepository`, `AppraisalSagaRepositoryAdapter`)
- Saga domain model (`AppraisalSagaRecord`, `AppraisalSagaRepository`)
- Old gateway interfaces and stubs (`PLUWGateway`, `PLAPRGateway`, `MasterpieceGateway`, `CustomerDBGateway` and their stubs)
- `MongoConfig.java` (MongoDB OffsetDateTime converters — not needed without saga persistence)
- Old `AppraisalController.java` (status-update endpoint, saga list/detail endpoints)

---

## What Was Built

### Two Camel Routes

1. **GetAppraisalList** (`GetAppraisalListRoute`) — scatter-gather
   - `POST /api/appraisals/list` with `{"policyNumber": "..."}`
   - Parallel fan-out: `direct:callAtWorkSQL` + `direct:callDEIPDE07MQList`
   - @Work branch: `AtWorkGateway` (fixture data, production: JDBC stored proc)
   - DEIPDE07 branch: send to `MQP.REQUESTQUEUE.1`, poll `MQP.RESPONSEQUEUE.1` via `AppraisalListMqPoller`
   - Aggregated + deduplicated by `AppraisalListAggregationStrategy`
   - `partialResult=true` when DEIPDE07 times out

2. **GetAppraisalDocument** (`GetAppraisalDocumentRoute`) — content-based router
   - `POST /api/appraisals/document` with `{"documentKey": "..."}`
   - Routes by key format: `_RiskID_I` → WCF insured, `_RiskID_A` → WCF agent, `^[0-9]{10,15}$` → DEIPDE07 MQ


# Decision: UC4 EDA Compliance — C3 AtWork Async Retrieval + I1 Saga Event Subscription

**Author:** DotNet  
**Date:** 2026-05-31T20:03:13-04:00  
**Status:** Implemented & verified (commit 96914d6)

---

## Summary

This decision completes the Architect's EDA compliance review for `dotnet-prs-appraisal`. Two remaining violations (C3 and I1) have been fully resolved.

---

## C3 Pattern — Async AtWork Retrieval via Pub/Sub

**Problem:** `DocumentRetrievalSaga` called `AtWorkFixture.BuildRetrievalResult()` inline on the AtWork path — synchronous infrastructure call inside a saga, causing temporal coupling.

**Fix:**  
- `DocumentRetrievalSaga` publishes `Uc4AppraisalDocumentRetrievalRequestedEvent` instead of calling the fixture inline.  
- New `AtWorkDocumentRetrievalHandler` subscribes to the event, calls the fixture, publishes `Uc4AtWorkDocumentRetrievedEvent`.  
- `DocumentRetrievalSaga` handles `Uc4AtWorkDocumentRetrievedEvent` via `TryCompleteAtWorkAsync()`, using the same TryComplete guard pattern as `DocumentListSaga`.

**Team convention established:** Any saga path that calls an external system must route via pub/sub event → handler → reply event. No direct infrastructure calls from saga handlers.

---

## I1 Pattern — Saga Started by Event, Not Command

**Problem:** `MainframeListAggregatorSaga` was started by `StartMainframeListAggregationCommand` sent by `MainframeDocumentListAdapterHandler`. The adapter handler subscribed to `Uc4AppraisalDocumentListRequestedEvent` and immediately forwarded a command — pure ceremony with no domain logic.

**Fix:**  
- `MainframeListAggregatorSaga` is now started directly by `Uc4AppraisalDocumentListRequestedEvent`.  
- `MainframeDocumentListAdapterHandler` deleted.  
- `StartMainframeListAggregationCommand` deleted.  
- `Program.cs` routing and `EDAFlowBehavior` participant map updated accordingly.

**Team convention established:** If a handler's only logic is "subscribe to event X, send command Y to myself", collapse to "saga started by event X directly". Commands exist for imperative intent; pub/sub events flow to all interested subscribers including sagas.

---

## Architect's EDA Review — All Violations Resolved

| ID | Finding | Status |
|----|---------|--------|
| C1 | DocumentListSaga AtWork inline call | ✅ Fixed prior session |
| C2 | Mainframe sub-saga not event-driven | ✅ Fixed prior session |
| C3 | DocumentRetrievalSaga AtWork inline call | ✅ Fixed this session |
| I1 | MainframeListAggregatorSaga started by command | ✅ Fixed this session |
| I2 | Infrastructure in domain layer | ✅ Fixed prior session |
| I3 | Missing completion event | ✅ Fixed prior session |

All 6 identified violations are resolved. `dotnet-prs-appraisal` is now fully EDA-compliant.
   - DEIPDE07 path: multi-message chunk aggregation via `PdfChunkMqPoller`; strips `\r\n` EBCDIC artifacts; detects `||END-OF-DOCUMENT||` sentinel
   - Returns `{"documentKey": "...", "base64Pdf": "...", "contentType": "application/pdf"}`

### Supporting Infrastructure

- `AppraisalListMqPoller` — `ConsumerTemplate` poll loop, 1s per-poll timeout, 30s max, sequence detection
- `PdfChunkMqPoller` — `ConsumerTemplate` poll loop, 1s per-poll timeout, 30s max, sentinel detection, CRLF strip
- `AppraisalListAggregationStrategy` — merges @Work + DEIPDE07 lists, deduplicates by (streetAdr + policyNumber)
- `AtWorkGateway` — demo fixtures for POL-001-TEST/POL-002-TEST/POL-003-TEST/default
- `RiskIdWcfGateway` — demo fixtures returning base64-encoded fake PDF content
- `JmsConfig` — wires Camel JMS component to Spring Boot Artemis `ConnectionFactory`
- `AppraisalAuditEventRoute` — publishes to `prs.events.appraisal-list-retrieved` and `prs.events.document-retrieved`

### pom.xml Additions

- `spring-boot-starter-artemis` (dev broker; production: IBM MQ ConnectionFactory)
- `camel-jms-starter`
- `camel-http-starter` (for future RiskID WCF production path)

### application.yml Changes

- Removed: saga timeout config, Kafka consumer config
- Added: `spring.artemis.*` connection config with env var overrides

---

## Architectural Notes

- **No persistence in this service** — both workflows are query-side, read-only, synchronous. MongoDB dependency retained in pom.xml (per existing project convention) but unused.
- **Production swap points**:
  - `AtWorkGateway`: replace fixture with JDBC `DataSource` call to `ESB_MWInterfaces_LC360_GetAppraisalList_MPC`
  - `RiskIdWcfGateway`: replace fixture with CXF/HTTP call to `GetInspectionPDFBizTalk` WCF endpoint
  - `JmsConfig`: replace Spring Artemis `ConnectionFactory` with IBM MQ `ConnectionFactory` bean — route DSL unchanged
- **Route IDs**: all kebab-case, all unique: `get-appraisal-list`, `call-atwork-sql`, `call-deipde07-mq-list`, `get-appraisal-document`, `call-riskid-wcf-insured`, `call-riskid-wcf-agent`, `call-deipde07-mq-document`, `publish-appraisal-list-audit`, `publish-document-retrieved-audit`


# Decision: UC4 Integration Test API Contract

**Author:** QA  
**Date:** 2026-05-30  
**Status:** Proposed  

---

## Decision

The UC4 integration tests for `prs-appraisal-service` establish the following REST API contract as executable specifications. The Integration agent must implement routes and a controller that satisfy these contracts before the tests can pass.

### Endpoint Contract: GetAppraisalList

```
POST /api/appraisals/list
Content-Type: application/json

Request:
{
  "policyNumber": "<string>"
}

Response (HTTP 200):
{
  "items": [
    {
      "appraisalUid":   "<string>",
      "policyQuoteNbr": "<string>",
      "streetAdr":      "<string>",
      "cityAdr":        "<string>",
      "stateCde":       "<string>",
      "zipAdr":         "<string>",
      "appraisalDte":   "<string>",
      "documenttype":   "<string>",
      "documentname":   "<string>",
      "documentkey":    "<string>"
    }
  ],
  "partialResult": <boolean>
}

Validation failure (empty policyNumber):
HTTP 400
```

### Endpoint Contract: GetAppraisalDocument

```
POST /api/appraisals/document
Content-Type: application/json

Request:
{
  "documentKey": "<string>"
}

Response (HTTP 200):
{
  "base64Pdf":   "<base64-encoded string — no \r\n>",
  "contentType": "application/pdf"
}

Unrecognised key format:
HTTP 400

Known key, document not found in backend:
HTTP 404
```

---

## Rationale

1. **Tests as executable specifications:** The UC4 REST API does not yet exist. By writing tests first against defined inputs/outputs, the team has a concrete, runnable contract the Integration agent can implement against.

2. **Integration profile isolation:** Tests are tagged `@Tag("integration")` and excluded from the default Maven test phase. This prevents CI failures while the implementation is in progress, while keeping the tests runnable on demand against the docker stack: `mvn test -pl prs-appraisal-service -Dgroups=integration`.

3. **`RestTemplate` over `@SpringBootTest`:** Tests call `http://localhost:8090` directly. This avoids spinning up a second application context and ensures the tests exercise the full running stack (Camel routes, JMS connections to Artemis, @Work SQL stub, RiskID WCF stub) — not an isolated Spring context.

4. **Timeout annotation on SC-004:** The `@Timeout(40)` guard on `timeout_deipde07_returnsPartialResult` is mandatory. Without it, if the MQ timeout implementation hangs, this test would block CI indefinitely. The 40-second ceiling gives the 30-second MQ timeout room to fire while preventing an infinite hang.

---

## Impact

- **Integration agent:** Must implement `GET /api/appraisals/list` and `GET /api/appraisals/document` routes in `prs-appraisal-service` that match the contracts above.
- **prs-appraisal-service pom.xml:** `spring-boot-starter-test` added in test scope; `integration-tests` Maven profile added. No impact on production build.
- **Test evidence:** `UC4TestEvidence.md` provides the fill-in table; QA will complete it after the first successful docker stack run.

### 41. UC4 dotnet-prs-appraisal rewrite (2026-05-31)
- Implement the rewrite with two SQL-persisted NServiceBus sagas (DocumentListSaga, DocumentRetrievalSaga), an in-memory TaskCompletionSource callback registry for HTTP request bridging, and Apache.NMS ActiveMQ background services that translate Artemis reply queues into UC4 completion events.
- Keeps the HTTP facade responsive while preserving the asynchronous integration pattern needed for mainframe and AtWork sources. Aligns the POC with the required queue names, reply parsing rules, and content-based routing behavior.
- The API now returns document-list and document-retrieval payloads for UC4 requests, and the legacy appraisal-status demo flow is removed from the service surface. Artemis connectivity is isolated behind a dedicated adapter/listener layer so the MQ simulation can later be swapped for IBM MQ with minimal saga/controller change.

### 42. UC4 Appraisals Page — API Proxy Convention (2026-05-31)
- The UC4 Appraisals demo page (`/uc4`) now uses two dedicated Next.js API proxy routes to forward requests to `prs-appraisal-service`:
  - `POST /api/appraisals/list` → `prs-appraisal-service:8090/api/appraisals/list`
  - `POST /api/appraisals/document` → `prs-appraisal-service:8090/api/appraisals/document`
- Both routes use a 35-second fetch timeout (30s MQ operation + 5s buffer), matching the backend's MQ scatter-gather and document retrieval timeouts.
- Service URL configured via `PRS_APPRAISAL_SERVICE_URL` environment variable (default: `http://prs-appraisal-service:8090`), added to `.env.local` as `http://localhost:8090` for local development.
- No changes to nav or layout; `/uc4` link was already present. Old saga/UpdateStatus UI fully removed. TypeScript passes clean.

### 43. UC4 MQ Queue Names and Request Contract (2026-05-31)
- UC4 appraisal MQ integration uses four dedicated queues instead of the legacy shared pair:
  - `APPRAISAL.LIST.REQUEST`
  - `APPRAISAL.LIST.REPLY`
  - `APPRAISAL.DOCUMENT.REQUEST`
  - `APPRAISAL.DOCUMENT.REPLY`
- `prs-appraisal-service` now sends exact pipe-delimited request payloads expected by `deipde07-mq-simulator`:
  - Appraisal list: `APPRAISAL_LIST|||{policyNumber}|||ACTIVE|||`
  - Document retrieval: `APPRAISAL_DOC|||{documentKey}|||`
- Both services read these queue names from explicit Spring properties / environment variables so local Docker and future deployments can override them without code changes.
- Separate listeners in the simulator make each flow own its response queue explicitly and avoid shared-queue branching logic.

### 44. Comprehensive UC4 EDA_FLOW hop logging in dotnet-prs-appraisal (2026-05-31)
- `dotnet-prs-appraisal` must emit a structured `EDA_FLOW` log entry for every sender→receiver hop in the UC4 appraisal document flow
- Update `EDAFlowBehavior` correlation extraction: try `AppraisalId`, then `CorrelationId`, then `RequestId`
- Extend `AppraisalParticipantMap` with UC4 document message destinations plus `AtWork` / `Mainframe` participant labels
- Add explicit `LogEdaFlow` helpers in HTTP controller, document sagas, and Artemis reply listeners for hops outside NServiceBus pipeline boundary
- Every entry standardized on observability contract: `EDA_Event`, `EDA_IssuanceId`, `EDA_MessageType`, `EDA_From`, `EDA_To`, `EDA_Direction`, `EDA_Stack`, `EDA_Topic`
- **Rationale:** Platform-ui flow tracer builds live sequence diagrams directly from Loki `EDA_FLOW` entries. UC4 document messages use `CorrelationId` / `RequestId` instead of `AppraisalId`, and critical hops happen outside NServiceBus behaviors, so tracer cannot render real flow unless every hop is logged with shared structured contract.

### 45. Dynamic participant derivation for Ops Flow Tracer (2026-05-31)
- Ops Flow Tracer keeps curated UC1 topology only as static fallback reference diagram
- When live `EDA_FLOW` events exist, sequence diagram derives participants directly from event `from` and `to` values instead of UC1-only mapping
- Pre-register known UC1 and UC4 participants in `PARTICIPANTS` for stable labels/colors
- Remove `LABEL_TO_ID`; live mode uses raw event participant IDs end-to-end
- Build `visibleParticipants` from unique participant IDs present in live events; unknown participants get runtime entries with rotating fallback colors
- Keep static mode on curated UC1 reference diagram for demo readability before live logs arrive
- Update `/ops` landing page copy to position tracer as generic correlation-driven flow viewer, not UC1-only issuance tool
- **Rationale:** UC4 and future flows introduce participant IDs not part of original UC1-only hardcoded list. Mapping unknown participants to `API` breaks diagram topology; participant identity must come from live event stream while preserving UC1 fallback for demos.

### 46. UC4 observability gaps — test coverage and EDAFlowBehavior fallback (2026-05-31)
- Identified release-significant gaps in UC4 observability acceptance for `dotnet-prs-appraisal`
- Existing Java UC4 integration tests are legacy-path only (target `POST /api/appraisals/list`, `POST /api/appraisals/document`)
- Current .NET primary path uses `GET /api/policies/{policyNumber}/appraisals/documents` and `GET /api/appraisals/documents/{documentKey}` with no dedicated test project
- `EDAFlowBehavior` currently only emits `EDA_IssuanceId` from `AppraisalId`, missing UC4 document messages that correlate by `RequestId` / `CorrelationId`
- Current `dotnet-prs-appraisal/Program.cs` uses plain-text console logging; frontend Loki route expects JSON with `EDA_*` fields parseable from top level or `Properties`
- **Action:** Add .NET UC4 tests for appraisal document endpoints; update `EDAFlowBehavior` to fall back to `CorrelationId` / `RequestId` for UC4 document messages; emit JSON console logs so platform-ui can parse `EDA_*` fields reliably

### 47. UC4 handler-invocation EDA_FLOW logging for subscriber fan-out (2026-05-31)
- For `dotnet-prs-appraisal`, subscriber-side `EDA_FLOW` observability will log at the NServiceBus `IInvokeHandlerContext` stage in addition to the existing incoming and outgoing pipeline behaviors
- `IIncomingLogicalMessageContext` fires once per received message, which collapses pub/sub fan-out into a single inbound edge. UC4 needs the ops sequence diagram to show the actual subscribers that handled an event
- Add `AppraisalEDAFlowHandlerInvokeBehavior : Behavior<IInvokeHandlerContext>`
- Resolve `EDA_To` from `context.MessageHandler.HandlerType.Name` through `AppraisalParticipantMap.ResolveHandler(...)`
- Emit `EDA_Direction = "handled"` and include raw handler class in `EDA_Handler`
- Keep existing incoming `consumed` and outgoing `published` logs; frontend can prefer `handled` entries when both exist
- Outgoing logs emit `EDA_Handler = "n/a"` to distinguish publish arrows from subscriber invocation arrows
- **Rationale:** Loki-backed flow tracer can render one arrow per actual subscriber for fan-out events without removing existing log streams or changing message contracts

### 48. UC4 Saga Panel from Flow Events (2026-05-31)
- When the standard policy saga endpoint returns no saga record but the live EDA flow matches UC4 appraisal traffic, the Ops flow tracer should derive the right-hand saga panel directly from Loki flow events instead of leaving the panel empty
- UC4 appraisal traces do not have useful data in the existing UC1 saga endpoint
- The live flow already contains enough milestones to show operator-friendly progress: request start, scatter-gather fan-out, branch completion, retrieval progress, and active handlers/subscribers
- Parse `EDA_Handler` and retain `handled` entries as first-class flow events
- Render separate subscriber arrows for handled fan-out deliveries and group consecutive sibling rows with a subtle fan-out bracket
- Show a ServicePulse-style compact timeline plus sub-saga summaries for UC4 when `saga == null && isUc4Flow(events)`
- **Rationale:** Keeps the UX useful while preserving the existing UC1 saga card for policy issuance flows


### 49. Per-service ingress hostnames replace path-based routing (2026-06-01)
**By:** Platform (requested by Steven Suing)

Backend APIs in AKS now use one hostname per service instead of sharing `api.middleware.internal` with path-based ingress rules.

| Service | Hostname |
|---|---|
| policy-issuance-service | `policy.middleware.internal` |
| platform-file-processing-service | `file-processing.middleware.internal` |
| platform-integration-service | `integration.middleware.internal` |
| prs-appraisal-service | `appraisal.middleware.internal` |

All hostnames resolve to the same ingress ILB (10.0.16.10) via Azure Private DNS A records (`infra/terraform/dns.tf`); ingress-nginx routes by Host header. Each service's ingress now uses a single `path: /` (Prefix) — Java controllers keep their full `@RequestMapping` paths unchanged; ingress no longer participates in URL routing.

**Why:** Path-based routing was fragile (paths drift between Java `@RequestMapping`, ingress rules, and APIM). One hostname per service makes the contract simpler: `Host` header == service identity.

**Scope / non-goals:**
- `ui.middleware.internal` unchanged (Frontend agent owns).
- `api.middleware.internal` DNS record + global helm comment retained until APIM backend cutover (now done — see #50).
- Internal-only services without ingress (customer-identity, billing-finance, platform-compliance, platform-notification) unaffected.
- `global.ingress.internalHost` retained for UI host pattern; no longer canonical for backend APIs.

**Validation:** `helm template` renders all 5 expected hosts; `terraform fmt -check` clean.

### 50. APIM per-host backend migration (2026-06-01)
**By:** Azure (requested by Steven Suing)

Migrated APIM from the single shared `aks-internal` backend (host `api.middleware.internal`) to **four per-API backends**, each pointing at its own internal hostname. Aligns APIM with the per-host ingress + private DNS topology from #49.

**Per-API `serviceUrl`** (path prefixes dropped — each app receives its full original path on its own host):

| API | New serviceUrl |
|---|---|
| policy-issuance-api | `https://policy.middleware.internal` |
| file-processing-api | `https://file-processing.middleware.internal` |
| platform-integration-api | `https://integration.middleware.internal` |
| prs-appraisal-api | `https://appraisal.middleware.internal` |

**Backends — Option A (per-API backends):**
- Created `apim/backends/{policy-issuance,file-processing,platform-integration,prs-appraisal}/backendInformation.json`
- Each API's `policy.xml` now does `<set-backend-service backend-id="{api-name}" />` instead of shared `aks-internal`
- Deleted `apim/backends/aks-internal/`

**Why Option A:** Each API's `policy.xml` already had explicit `<set-backend-service>` overriding `serviceUrl` at runtime. Just changing `serviceUrl` would have been a silent no-op without also updating `policy.xml`. Per-API backends are clearer, give a single place to tune TLS/credentials/circuit-breaker per service, and keep the APIM portal showing the right backend per API.

**Untouched (intentional):**
- `apim/apiops.yml` `backend.url: https://api.middleware.internal` is the apiops pipeline default config block, not a deployed APIM resource. Left alone to avoid risk to apiops sync workflow.
- Operation `urlTemplate` values unchanged — APIM concatenates `serviceUrl + urlTemplate`.

**Cross-team coordination:** Assumes Platform's per-host ingress preserves the original path prefix (`/api/v1/policies`, `/api/v1`, `/api`, `/api/appraisals`) to the pod, OR the apps accept short paths. Per #49, ingress is now `path: /` and Spring/.NET controllers keep full `@RequestMapping` paths — contracts align.

**Validation:** Grep of `api.middleware.internal` under `apim/` shows only the 1 intentional `apiops.yml` reference. Each API's `serviceUrl` matches its new backend `url` and DNS A record.

### 51. .NET HTTP services use `dotnet-` prefixed per-host ingress (2026-06-01)
**By:** Steven Suing (via Platform)

The two .NET services with HTTP APIs (`dotnet-policy-issuance`, `dotnet-file-processing`) onboard onto the same per-host ingress + private DNS pattern as their Java counterparts, but with a `dotnet-` hostname prefix to disambiguate from the Java hosts that already own `policy.middleware.internal` and `file-processing.middleware.internal`.

- `dotnet-policy-issuance` → `dotnet-policy.middleware.internal`
- `dotnet-file-processing` → `dotnet-file-processing.middleware.internal`

Both A records resolve to the shared ingress ILB (`10.0.16.10`); ingress-nginx routes by Host header. The other 7 .NET services are event-only (NServiceBus consumers) and remain cluster-internal — no Service.type=LoadBalancer, no ingress, no DNS.

**Why:** Project goal is running Java and .NET stacks side-by-side for parity comparison. Reusing the bare hostnames would force a stack switch via APIM-only routing; prefixing the .NET hosts lets both stacks be addressable simultaneously through APIM and from in-cluster clients.

**Files changed:**
- `helm/middleware/values.yaml` — added `ingress:` blocks for the 2 services only
- `infra/terraform/dns.tf` — added `dotnet_policy` and `dotnet_file_processing` A records
- `infra/terraform/network.tf` — extended the DNS-records comment block

**Validation:** `helm template` renders both hosts; `terraform fmt -check` clean.

### 52. APIM dual-stack onboarding for .NET HTTP services (2026-06-01)
**By:** Steven Suing (via Azure)

Added two new APIM APIs and two new APIM backends so the .NET policy-issuance and file-processing services are reachable through APIM alongside their Java counterparts. Both stacks live concurrently; clients pick stack via the URL prefix.

- New APIs: `apim/apis/dotnet-policy-issuance-api/`, `apim/apis/dotnet-file-processing-api/` — mirror Java siblings; specification.yaml copied verbatim (routes identical).
- New backends: `apim/backends/dotnet-policy-issuance/`, `apim/backends/dotnet-file-processing/` pointing at `https://dotnet-policy.middleware.internal` and `https://dotnet-file-processing.middleware.internal`.
- Each new API's `policy.xml` does `<set-backend-service backend-id="dotnet-{name}" />` per decision #50.

**Naming convention:** `dotnet-` PREFIX uniformly: folder name, APIM displayName suffix `(.NET)`, APIM `path`, backend id, ingress hostname. Java stack keeps bare names — no churn on existing clients. `apim/apis/dotnet-*`, `apim/backends/dotnet-*`, `path: dotnet-*`, `backend-id: dotnet-*`, host: `dotnet-*.middleware.internal`.

**Why dual-stack instead of cutover:** Stacks need to run side by side during BizTalk → modern-platform migration so traffic can be split, compared, and rolled back per-API without touching APIM consumer subscriptions. Path-based split (`/policy-issuance` vs `/dotnet-policy-issuance`) keeps both reachable through the same APIM instance, product, and subscription model — only the URL prefix differs.

**Why no APIs for the 7 event-only .NET services:** kafka-bridge, customer-identity, billing-finance, platform-compliance, platform-integration, platform-notification, prs-appraisal expose no HTTP surface — they're Kafka consumers/producers. APIM façade with nothing behind it is meaningless.

**Out of scope (owned by Platform):** ingress hostnames + DNS A records + AKS Service/Ingress (delivered in #51).

**Validation:** Grep of `apim/` confirms each new hostname appears in exactly two places (its API's `serviceUrl` + its backend's `url`). All six APIM `path` values unique — no Java/.NET collision.

### 53. Grafana on AKS — anonymous Viewer for v1 (2026-06-01)
**By:** Steven Suing (via Platform)

AKS Grafana deployed via `grafana/grafana` Helm chart with `auth.anonymous.enabled: true` and `org_role: Viewer`. No login required to view dashboards at `https://grafana.middleware.internal`.

**Why:** Matches local docker-compose posture; lets the team start using Grafana immediately without waiting on AAD app registration. Cluster is internal-only (private DNS zone, internal ILB) so the only readers reach it through the corporate VNet.

**Risk:** Anyone on the VNet can read all dashboards. No write access (Viewer role). Acceptable for v1.

**TODO follow-up:** Replace with Entra ID OAuth (`auth.generic_oauth`) once Azure provisions an AAD app registration with the Grafana client ID + secret in Key Vault. Switch `auth.anonymous.enabled` to `false`, set `org_role: Editor` for an admin group, and restrict editor access by AAD group claim.

### 54. Observability stack landed in AKS umbrella chart (2026-06-01)
**By:** Steven Suing (via Platform)

Added Kafdrop + Loki + Promtail + Prometheus + Tempo + Otel Collector + Grafana to `helm/middleware`. Community charts pinned as dependencies; Kafdrop is a custom subchart at `helm/charts/kafdrop/` (no community chart wires Event Hubs JAAS). All components use `fullnameOverride` so service DNS names are deterministic: `middleware-loki:3100`, `middleware-prometheus:80`, `middleware-tempo:3200`, `middleware-otel-collector:4317`, `middleware-grafana`, `middleware-kafdrop`.

**Why:** Mirror the local docker-compose observability stack in cloud so we can debug AKS issues with the same tooling we use locally.

**Storage:** Ephemeral PVCs on `managed-csi` (Loki 10Gi, Prometheus 20Gi, Tempo 10Gi, Grafana 5Gi). NOT Azure Blob — deferred (see #55).

**Auth:** Grafana anonymous Viewer for v1 (#53). Entra ID OAuth deferred.

**Ingress:** Two new internal hostnames — `kafdrop.middleware.internal`, `grafana.middleware.internal`. Loki/Prometheus/Tempo stay cluster-internal. DNS records added in `infra/terraform/dns.tf`.

**Global OTEL endpoint:** `global.otel.endpoint` updated to `http://middleware-otel-collector:4317`. All Java/.NET services already reference `{{ .Values.global.otel.endpoint }}` so no per-service edits required.

**Prometheus discovery:** Added `prometheus.io/scrape|path|port` pod annotations to the shared `microservice` deployment template, gated on `stack == "java"` so Spring Boot Actuator endpoints get auto-scraped and .NET pods are skipped.

**UI:** Added `NEXT_PUBLIC_KAFDROP_URL` and `NEXT_PUBLIC_GRAFANA_URL` to `platform-ui.env` so Frontend can wire the links without touching the chart.

### 55. Observability storage TODO — switch to Azure Blob backends (2026-06-01)
**By:** Steven Suing (via Platform)

Loki/Prometheus/Tempo on AKS run with ephemeral PVCs on `managed-csi` (10/20/10 Gi respectively). On pod loss, history is lost. v1 expedience.

**TODO follow-up:** Switch all three to Azure Blob backends:
- **Loki:** `storage.type: azure` with a dedicated container in `stmiddleware{env}observability` (Azure provisions the storage account + container + Workload Identity role assignment `Storage Blob Data Contributor` on the container).
- **Prometheus:** enable `prometheus-community` remote write to long-term store (Azure Monitor managed Prometheus is the natural target — keeps the Prometheus pod local-only for live queries and ships durability to AMW).
- **Tempo:** `storage.trace.backend: azure` with its own container.

**Blocker:** Azure must publish the storage account name + Workload Identity client ID with the right role binding before this is actionable.

### 56. Kafdrop → Azure Event Hubs auth recipe (2026-06-01)
**By:** Steven Suing (via Azure)
**Audience:** Platform (deploying Kafdrop into AKS)
**Full design:** see decisions inbox archive / chart README

**Choice:** Option B — SAS connection string + SASL_PLAIN, secret pulled from Key Vault via CSI driver. Reject Option A (Workload Identity + OAUTHBEARER) for the POC: requires a custom Kafdrop image with `azure-identity-extensions` jars and a custom callback class. Kafdrop is a read-only diagnostic UI; the security delta between a namespace-scoped Listen-only SAS key and a federated workload identity is small relative to the operational cost of maintaining a forked image.

**Pre-flight blocker (Platform/Network):** Event Hubs Kafka endpoint listens on **TCP 9093 only**. Confirm AKS subnet egress allows TCP/9093 to `*.servicebus.windows.net` (or service tag `EventHub`) before rolling out Kafdrop. Same constraint applies to all existing Java services using SASL_SSL.

**Auth wiring:**
- `KAFKA_BROKERCONNECT`: `<eventhubs-namespace>.servicebus.windows.net:9093`.
- `KAFKA_PROPERTIES` rendered at pod start from CSI-mounted secret (`eventhubs-kafdrop-connection-string`) — never baked into chart or image.
- JAAS: `org.apache.kafka.common.security.plain.PlainLoginModule required username="$ConnectionString" password="<conn-string>";` — `username` is the literal `$ConnectionString` (Event Hubs convention).
- SAS policy: `kafdrop-listen` at namespace scope with **Listen** claim only — never the root key.

**Service Account:** existing `middleware-workload` (no Entra calls under Option B).

**Federated identity / RBAC:** none required for Option B. SAS policy authorization is enforced by the connection string itself.

**Terraform additions (Azure):**
- `azurerm_eventhub_namespace_authorization_rule.kafdrop_listen` — namespace-scoped Listen-only SAS policy.
- `azurerm_key_vault_secret.kafdrop_eh_conn` — pushes `primary_connection_string` into Key Vault as `eventhubs-kafdrop-connection-string`.

**JVM/memory:** keep DevOps Decision #43 — `JVM_OPTS: "-Xms32M -Xmx128M"` with `mem_limit: 384m`. No additional JVM flags required for SASL_SSL/PLAIN; bundled kafka-clients trusts the DigiCert chain.

**Migration path to Option A (if audit demands no shared secrets):** custom Dockerfile from `obsidiandynamics/kafdrop:4.0.2`, drop `azure-identity-extensions` + `azure-identity` + `azure-core` jars into `/app/BOOT-INF/lib/` of the fat jar (not `loader.path` — Kafdrop 4.0.2 uses `JarLauncher`, ignores it). Then switch to `sasl.mechanism=OAUTHBEARER` + `KafkaOAuth2AuthenticateCallbackHandler`, give Kafdrop its own SA `kafdrop-workload` with federated credential and `Azure Event Hubs Data Receiver` role at namespace scope.
