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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
