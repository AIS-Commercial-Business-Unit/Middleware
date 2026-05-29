# Squad Decisions

## Active Decisions

### 1. Persistence Package Convention
- All MongoDB-specific code lives in `persistence/` subpackage within each domain service
- Domain classes remain infrastructure-free (no Spring/MongoDB annotations)
- Repository interfaces defined in domain, implemented as adapters in persistence layer
- **Impact:** Can swap MongoDB for PostgreSQL/Cassandra without touching domain code

### 2. DLQ Topic Naming: `{domain}.dlq.{route-name}`
- Standard pattern across all services
- Replaces inconsistent naming (`*-failures`, `check-failures`)
- Retry policy: 3 attempts with 1-2 second base delay, exponential backoff (2x multiplier)
- **Decision drivers:** Consistency, traceability, standardized error handling

### 3. Atomic Join Condition in IssuanceSagaRoute
- MongoDB `findAndModify()` with `returnNew(true)` ensures exactly one thread sees both flags set
- CAS on status field prevents duplicate `PolicyIssued` events
- **Impact:** Eliminates race condition where concurrent billing + customer events would cause saga to hang

### 4. Topic Naming: `file.events.*` (not `fileprocessing.*`)
- Single-word domain segments across all services
- Consistent with `policy`, `compliance`, `customer`, `billing`, `integration`, `notification`
- Migrated from `fileprocessing.events.*` to `file.events.*`

### 5. File Polling Rate: `maxMessagesPerPoll=10`
- Prevents heap exhaustion on startup or burst file arrival
- Processes at most 10 files per 5-second poll cycle
- Allows downstream Kafka consumers time to drain

### 6. Non-Root Containers
- All 14 Java service Dockerfiles + stubs run as non-root `appuser`
- Security hardening across entire container fleet
- Applied consistently across domain services, stubs, and platform-ui

### 7. Health Checks + Dependency Ordering
- All services have `healthcheck:` blocks with appropriate probes
- `depends_on` uses `condition: service_healthy` for startup order
- `kafka-setup` pre-creates 24 topics before domain services start
- Spring Boot services use 60s start period for JVM warm-up

### 8. Grafana Admin Password via Environment Variable
- `GF_SECURITY_ADMIN_PASSWORD` can be overridden via `.env`
- Defaults to `admin` for local development
- Documented in `.env.example`

### 9. Frontend: UC3 File Processing Proxy with `[...path]`
- Catch-all route segment for file-processing API forwarding
- Supports GET/POST; PUT/DELETE/PATCH to be added if service requires them
- Batch detail page uses SWR `refreshInterval` as function to stop polling at terminal state

### 10. MDC Context in All Routes
- `issuanceId` (or `correlationId` in satellite services) now populated in all route entry points
- Enables distributed tracing across service logs via structured JSON output
- Applied to BillingAssociationRoute, ComplianceCheckRoute, AccountServiceRoute

### 11. MongoDB Indexes on Query Fields
- Added `@Indexed` to: `BatchRecordDocument.batchId`, `BatchRecordDocument.correlationId` (unique), `IssuanceSagaDocument.batchId`, `ComplianceCheckDocument.correlationId`
- Eliminates full collection scans on frequently-queried fields
- Performance improvement for all lookups from RenewalBatchRoute and other consumers

### 12. Kafka Auto-Create Disabled, Explicit Setup Service
- `KAFKA_AUTO_CREATE_TOPICS_ENABLE: false` prevents uncontrolled topic creation
- `kafka-setup` service now owns topic creation (24 topics, 3 partitions, RF=1, all DLQ topics)
- Deterministic, audit-able topic provisioning

### 13. ProducerTemplate Lifecycle Management
- Injected as singleton bean (not per-call `createProducerTemplate()`)
- Prevents resource leaks under load
- Applied to RecordOutcomeRoute and FileArrivalRoute

### 14. Global Exception Handler for REST APIs
- `@RestControllerAdvice` added to platform-file-processing-service and policy-issuance-service
- Standardizes error response format (400/500 with `{"error": "..."}`)
- Handles IllegalArgumentException, MethodArgumentNotValidException, and catch-all Exception

### 15. NServiceBus Saga Persistence: MongoDB (Not SQL Server)
- MongoDB persists .NET/NServiceBus saga state (same as Java stack)
- SQL Server is used only as NServiceBus transport for message queuing
- Clarification: Both Camel and NServiceBus stacks use MongoDB for saga/domain persistence
- **Decision drivers:** Unified persistence model across tech stacks, SQL Server relegated to transport

### 16. EDA Events vs Commands Discipline (2026-05-27)
- A service NEVER commands another service to do what it should subscribe to. Use Publish → Subscribe, not Send → Handle.
- Commands are for entry points only: user-facing API → first domain service. Never between domain services.
- Events communicate state changes. Domain services subscribe to events and act autonomously.
- **Reference:** Udi Dahan principle; https://docs.particular.net/nservicebus/messaging/messages-events-commands
- **Applied to:** Java UC1 Policy Issuance flow and .NET UC1 Policy Issuance flow (reworked both stacks)

### 17. Java UC1 EDA events-vs-commands fix (2026-05-27)
- `IssuanceSagaRoute` now publishes `PolicyIssuanceInitiatedEvent` and `AccountLookupRequestedEvent` instead of commanding downstream services
- BillingAndFinance and CustomerIdentity now subscribe directly to `integration.events.policy-admin-system-response-received` (fan-out event carries `accountServiceRequestNumber`)
- Restores Udi Dahan / NServiceBus EDA semantics across Java stack

### 18. DotNet UC1 EDA command-to-event correction (2026-05-27)
- `IssuanceSaga` now publishes `PolicyIssuanceInitiatedEvent` instead of sending `RequestComplianceCheckCommand`
- `IssuanceSaga` now publishes `AccountLookupRequestedEvent` instead of sending `GetOrCreateAccountServiceRecordCommand`
- `IssuePolicyRequestedEvent` remains PAS trigger and carries `AccountServiceRequestNumber`
- `PasGatewayHandler` subscribes to `IssuePolicyRequestedEvent`, republishes `PolicyAdminSystemResponseReceivedEvent` with `AccountServiceRequestNumber`
- Saga records progress and waits for completion events instead of commanding external side effects

### 19. Backend EDA flow logging for policy-issuance-service (2026-05-27)
- Add `EDAFlowProcessor` in `com.ais.middleware.policy.issuance.observability` (Java)
- Wire into `IssuanceSagaRoute` with `interceptFrom("kafka:*")` for consumes and `interceptSendToEndpoint("kafka:*")` for publishes
- Enrich MDC with: `EDA_Event`, `EDA_IssuanceId`, `EDA_MessageType`, `EDA_From`, `EDA_To`, `EDA_Topic`, `EDA_Direction`, `EDA_Stack`
- Guarantees consistent logging for every route-level Kafka hop in the orchestrator
- Keeps observability concerns out of saga/domain processors

### 20. DotNet EDA_FLOW NServiceBus pipeline logging (2026-05-27)
- Emit structured `EDA_FLOW` Serilog entries from `dotnet-policy-issuance` NServiceBus pipeline behaviors
- Added `EDAFlowIncomingBehavior` for consumed messages, `EDAFlowOutgoingBehavior` for published/sent messages
- Same `EDA_*` property contract across stacks (event, issuanceId, messageType, from, to, direction, stack, topic)
- Registered in `Program.cs` before `NServiceBus.Endpoint.Start()` using Serilog-backed `LoggerFactory`
- Endpoint-to-participant mapping provides stable labels for Loki queries (e.g., `PolicyIssuance`, `Compliance`, `CustomerIdentity`)

### 21. Frontend runtime backend switcher + UC1 diagram correction (2026-05-27)
- Platform UI chooses active Java/.NET backend at request time via `active-backend` cookie
- Added `platform-ui/src/app/api/backend/route.ts` to read/write backend cookie
- Updated proxy routes to resolve backend targets from request cookie, not build-time env only
- Added `BackendSwitcher` client island in top nav for runtime stack flipping
- Corrected ops sequence diagram to mark all steps on `Completed`, replaced command arrows with event-only UC1 topology
- PAS fan-out and parallel completion events now reflected in diagram

### 22. Dynamic sequence diagram from Loki EDA_FLOW (2026-05-27)
- UC1 ops page prefers live `EDA_FLOW` events from Loki for sequence diagram
- Falls back to curated static topology when no flow events available yet
- Added `platform-ui/src/types/eda-flow.ts` for typed `FlowEvent` contract
- Added `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` Loki proxy route
- Loki proxy parses structured `EDA_FLOW` JSON, normalizes Java/.NET payloads, deduplicates edges, returns sorted events
- Updated `platform-ui/src/app/ops/[issuanceId]/page.tsx` to poll flow endpoint with live/static badge
- Reuses existing participant columns and SVG renderer for consistency

### 23. Udi Dahan as EDA authority (2026-05-27)
- Whenever architectural questions about event-driven architecture arise, research Udi Dahan's opinion
- Follow Udi Dahan principles as guiding authority for EDA design decisions on this project
- Reference: https://docs.particular.net/nservicebus/messaging/messages-events-commands

### 24. Backend EDA flow logging in all Java services (2026-05-27)
- Extend Java `EDA_FLOW` observability beyond `policy-issuance-service` to all satellite services
- Add `EDAFlowProcessor` to each service: `platform-compliance-service`, `customer-identity-service`, `platform-integration-service`, `billing-finance-service`, `platform-notification-service`
- Wire `interceptFrom("kafka:*")` and `interceptSendToEndpoint("kafka:*")` at route builder level
- Keep observability at Camel route boundary, out of domain processors
- **Verification:** All five services instrumented, containers rebuilt and deployed, verified complete EDA_FLOW entries for policy issuance end-to-end

### 25. DotNet UC1 flow parity (2026-05-27)
- Use `.docs/req/use-cases.html` as source of truth for UC1 participant/message ordering
- Treat `.NET saga as event-driven, render canonical flow through `EDA_FLOW` labels
- Map UC1 edges explicitly in `EDAFlowBehavior`: IssuePolicyCommand, PolicyIssuanceInitiatedEvent, AccountLookupRequestedEvent, IssuePolicyRequestedEvent, PolicyIssuedEvent
- Preserve canonical publish/subscribe semantics + add SQL transport reliability fallbacks
- Seed `dbo.SubscriptionRouting` deterministically, start Integration after main .NET subscribers are healthy
- Publish `PolicyAdminSystemResponseReceivedEvent` then direct-send to policy-issuance, billing, customer
- **Verification:** Build passed, all 6 tests passed, live issuance 232eb4f4 reached Completed with canonical flow

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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
