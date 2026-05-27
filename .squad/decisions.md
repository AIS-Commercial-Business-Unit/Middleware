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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
