# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Java (Spring Boot or Quarkus), MongoDB, REST APIs, OpenAPI specs, SLF4J structured logging
- **Key principle:** Repository interfaces in domain layer; MongoDB implementation in infrastructure layer; domain never imports infrastructure types; correlation IDs on all logs
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-28 — UC4 prs-appraisal-service built (backend-5 complete)

- **New service `prs-appraisal-service` created at `java/prs-appraisal-service/`** (port 8090). Follows identical DDD layering as policy-issuance-service: domain/ (pure Java), application/gateway/ (stub impls), persistence/ (MongoDB adapter), routes/ (Camel), observability/ (EDAFlowProcessor), api/ (REST controller).

- **AppraisalReceivedSaga with sub-workflow routing:** Single Kafka topic `prs.events.appraisal-received` is the entry point. After ProducerLookupRequested/Retrieved handoff, the saga branches on `statusCode`: 6 → StatusCode6UWSagaRoute (parallel PLUW + UW determination + join), 15 → StatusCode15CompletedSagaRoute, other → GenericStatusUpdateSagaRoute. Each route is a separate `@Component`-annotated `RouteBuilder`.

- **Parallel join pattern for StatusCode=6:** Both PLUWAppraisalCreated and UWAssignmentDetermined publish to separate Kafka topics. The `appraisal-received` service subscribes to both and uses MongoDB `findAndModify` with `ne(status, UpdatingDownstream)` CAS to ensure exactly one join thread proceeds — same atomic join pattern as IssuanceSagaRoute.

- **Timeout via timer watchdog:** No NServiceBus-equivalent timeout callbacks in Camel. Implemented as a `timer:saga-timeout-watchdog?period=60000` Camel timer route scanning MongoDB for stale sagas past their `timeoutAt` field. ProducerTemplate (injected singleton, not created inline) publishes to failure and DLQ topics.

- **Gateway stubs log ⚠️ DEMO GAP / STUBBED markers:** All five gateways (PLUW, PLAPR, Masterpiece, CustomerDB, RiskIDMQ-as-HTTP) emit `log.warn("⚠️ STUBBED: ...")` and `log.warn("⚠️ DEMO GAP: ...")` on every call. REPLACE_ME_ prefixes on all fabricated data fields. These are visible to the demo audience in logs and the Platform UI.

- **Customer-identity-service extended:** Added `ProducerLookupRoute.java` subscribing to `prs.events.producer-lookup-requested`, returning `prs.events.producer-crossref-retrieved` with in-memory REPLACE_ME_ lookup table. Reuses the same EDAFlowProcessor already present in that service.

- **9 new UC4 events added to common module** in `com.ais.middleware.common.events.prs.*`: ProducerLookupRequestedEvent, ProducerCrossReferenceRetrievedEvent, PLUWAppraisalCreateRequestedEvent, PLUWAppraisalCreatedEvent, UWDeterminationRequestedEvent, UWAssignmentDeterminedEvent, AppraisalUnderwriterAssignedEvent, AppraisalCompletedEvent, AppraisalStatusUpdateFailedEvent.

- **13 new Kafka topics** added to docker-compose.yml kafka-setup (prs.events.* and prs.dlq.*). prs-appraisal-service added to docker-compose.yml as a domain service on port 8090 with MongoDB DB `prs_appraisal_db`.

- **Parent pom.xml updated** to include `prs-appraisal-service` as a module in the Maven reactor.

- **Maven build verified clean** via Docker container (`docker run maven:3.9-eclipse-temurin-21`): `mvn clean package -DskipTests -pl common,prs-appraisal-service,customer-identity-service -am` exits 0.

- **Key file paths:**
  - `java/prs-appraisal-service/` — new service root
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/AppraisalReceivedSagaRoute.java` — main saga
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/StatusCode6UWSagaRoute.java` — UW determination parallel flow
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/api/AppraisalController.java` — REST entry + saga list/detail
  - `java/customer-identity-service/src/main/java/com/ais/middleware/customer/identity/routes/ProducerLookupRoute.java` — UC4 producer lookup
  - `java/prs-appraisal-service/src/main/resources/demo-test-scenarios.md` — Postman scenarios

### 2026-05-27 — EDA flow logging across all Java services (backend-4 complete)

- **Satellite services now have EDAFlowProcessor instrumentation.** All five downstream services (Compliance, Customer Identity, Integration, Billing, Notification) now emit route-boundary Kafka `EDA_FLOW` logs identical to policy issuance. This enables the live ops sequence diagram to render the complete fan-out topology including Integration→PolicyAdminSystemResponseReceivedEvent→Billing and Customer Identity consumption paths.

- **Verification:** Issuance 2321192b-72dd-497b-8439-37f2e7c349a9 traced through Compliance, Customer Identity, Integration (PAS), and Billing with all `EDA_FLOW` entries confirmed. The sequence diagram now renders the canonical Java EDA flow end-to-end.

- **Satellite services need the same route-boundary Kafka interceptors as policy issuance.** Adding `interceptFrom("kafka:*")` and `interceptSendToEndpoint("kafka:*")` with a local `EDAFlowProcessor` in Compliance, Customer Identity, Integration, Billing, and Notification makes the live sequence diagram show real fan-out hops instead of only orchestrator-side edges.

- **Outbound topic extraction must normalize both `kafka:topic` and `kafka://topic`.** Reusing `uri.replaceFirst("^kafka:(//)?", "")` plus query-string trimming keeps `EDA_Topic` correct for both direct `.to("kafka:...")` and intercepted endpoint URIs.

- **Satellite services need the same route-boundary Kafka interceptors as policy issuance.** Adding `interceptFrom("kafka:*")` and `interceptSendToEndpoint("kafka:*")` with a local `EDAFlowProcessor` in Compliance, Customer Identity, Integration, Billing, and Notification makes the live sequence diagram show real fan-out hops instead of only orchestrator-side edges.

- **Outbound topic extraction must normalize both `kafka:topic` and `kafka://topic`.** Reusing `uri.replaceFirst("^kafka:(//)?", "")` plus query-string trimming keeps `EDA_Topic` correct for both direct `.to("kafka:...")` and intercepted endpoint URIs.

- **These Java service Dockerfiles package prebuilt `target/*.jar` files rather than compiling during image build.** In environments without a local JDK/Maven, the reliable fallback is to package the modules in a Maven container first, then rebuild the service images so the new observability code is actually present in the containers.

### 2026-05-28 — UC4 prs-appraisal-service & cross-stack alignment

- **AppraisalReceivedSaga implementation complete** — New `prs-appraisal-service` on port 8090 with identical DDD structure as policy-issuance-service. 4 saga routes: AppraisalReceivedSagaRoute (outer), StatusCode6UWSagaRoute (parallel join), StatusCode15CompletedSagaRoute, GenericStatusUpdateSagaRoute. Domain layer completely infrastructure-free; all persistence adapters in infrastructure layer.

- **StatusCode6 parallel join pattern mirrors UC1 IssuanceSagaRoute** — Two separate Kafka subscriptions (`prs.events.pluw-appraisal-created` + `prs.events.uw-assignment-determined`) update MongoDB saga document with `findAndModify` + CAS on status field. Exactly one route proceeds when both boolean flags are true. Same atomic pattern prevents race condition on both UC1 (policy) and UC4 (appraisal).

- **Saga timeout via timer watchdog (Camel-specific pattern)** — `SagaTimeoutRoute` with `timer:saga-timeout-watchdog?period=60000` scans MongoDB for sagas where `timeoutAt < now()` and status not terminal. Uses `findAndModify` with `nin(status, [Completed, Failed, TimedOut])` CAS to prevent double-processing. ProducerTemplate injected as singleton bean per Decision #13. Timeout duration tunable via `appraisal.saga.timeout-minutes` in application.yml.

- **HTTP controller stub for RiskIDMQGateway entry point** — `AppraisalController.POST /api/appraisal/status-update` accepts simplified request and publishes `AppraisalReceivedEvent` to Kafka. Gateway pattern visible in logs with `⚠️ DEMO STUB: RiskIDMQGateway HTTP endpoint called`. At production time, replace controller body with `from("jms:queue:RISKID.STATUS.UPDATE...")` in `RiskIDGatewayRoute` — zero saga logic changes needed.

- **5 gateway interfaces with stub adapters in infrastructure** — IRiskIDMQGateway, IPLUWGateway, IPLAPRGateway, IMasterpieceGateway, ICustomerDBGateway all defined in domain layer, implemented as stubs with `REPLACE_ME_*` constants. Every call logs `⚠️ DEMO STUB:` so demo audience sees exact integration boundaries.

- **All 9 UC4 events added to common** — ProducerLookupRequestedEvent, ProducerCrossReferenceRetrievedEvent, PLUWAppraisalCreateRequestedEvent, PLUWAppraisalCreatedEvent, UWDeterminationRequestedEvent, UWAssignmentDeterminedEvent, AppraisalUnderwriterAssignedEvent, AppraisalCompletedEvent, AppraisalStatusUpdateFailedEvent in `com.ais.middleware.common.events.prs.*` package.

- **13 new Kafka topics** — prs.events.* (appraisal-received, pluw-appraisal-created, uw-assignment-determined, producer-lookup-requested, producer-crossref-retrieved, appraisal-underwriter-assigned, appraisal-completed, appraisal-status-update-failed) plus prs.dlq.* (riskid-gateway, timeout). All pre-created by kafka-setup service before domain services start.

- **Customer-identity-service extended, not replaced** — Added `ProducerLookupRoute` subscribing to `prs.events.producer-lookup-requested`, publishing `prs.events.producer-crossref-retrieved` with in-memory REPLACE_ME_ lookup table. Reuses existing EDAFlowProcessor in that service. No existing UC1 routes or sagas touched.

- **Maven build verified clean** — `mvn clean package -DskipTests -pl common,prs-appraisal-service,customer-identity-service -am` exits 0. Parent pom.xml updated with prs-appraisal-service as reactor module. Docker image build succeeds with no compile errors.

- **8 identified demo gaps documented in code** — All 8 unknown requirements (IBM MQ schema, PLUW WCF contract, PLAPR procedure, Masterpiece format, CustomerDB structure, UW rules, suspense days, active status codes) marked with `// ⚠️ DEMO GAP:` comments in event classes and routes. Searchable for PRS team review.

- **Cross-stack alignment with .NET prs-appraisal** — Both Java and .NET AppraisalReceivedSaga implement identical flow: outer orchestrator → StatusCode6 parallel join (atomic via MongoDB CAS or NServiceBus join mechanism), StatusCode15 sequential, generic pass-through. Same 5 gateways with same names. Frontend runtime backend switcher tests both implementations against identical demo data.

- **EDAFlowProcessor picks up appraisalId via correlationId fallback** — UC4 correlation key is `appraisalId` (not `issuanceId`). Stored as both `correlationId` exchange property and header so existing `EDAFlowProcessor.resolveIssuanceId()` fallback picks it up without changes. Live ops sequence diagram will render UC4 saga flow alongside UC1 flow when backend is switched.

- **Key file paths:**
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/` — service root
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/{AppraisalReceivedSagaRoute, StatusCode6UWSagaRoute, StatusCode15CompletedSagaRoute, GenericStatusUpdateSagaRoute, SagaTimeoutRoute}.java`
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/gateway/{IRiskIDMQGateway, IPLUWGateway, ...}.java` (interfaces)
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/gateway/adapter/` (stub implementations)
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/persistence/` (MongoDB documents and repositories)
  - `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/api/AppraisalController.java`
  - `java/customer-identity-service/src/main/java/com/ais/middleware/customer/identity/routes/ProducerLookupRoute.java`
  - `java/common/src/main/java/com/ais/middleware/common/events/prs/` (9 UC4 events)

### 2026-05-27 — Policy issuance EDA flow logging

- **Camel Kafka flow logging should sit at the route boundary, not in domain processors.** `interceptFrom("kafka:*")` + `interceptSendToEndpoint("kafka:*")` lets policy-issuance-service emit one structured `EDA_FLOW` record per consumed/published Kafka message without leaking observability code into saga business logic.

- **Kafka endpoint URIs may arrive as `kafka://topic` during send interception.** Topic extraction for outbound Camel intercepts must normalize both `kafka:topic` and `kafka://topic`; otherwise MDC ends up with malformed topics like `//policy.events...` and participant/message-type lookups fail.

- **Current logback JSON config was allow-listing MDC keys.** Because `logback-spring.xml` explicitly listed MDC fields, the new `EDA_*` keys had to be added there for Loki queries to see them.

- **Cross-stack observability contract enables live sequence diagrams in frontend.** Once Java EDAFlowProcessor and .NET EDAFlowBehavior both emit `EDA_*` structured logs to Loki, the frontend Loki proxy can query and parse them into a true live topology diagram that validates actual message flow against the publish/subscribe architecture. This creates a feedback loop where operational visibility reinforces Udi Dahan's EDA rules.

### 2026-05-27 — Java EDA events-vs-commands fix

- **Sagas publish facts; subscribers act.** In UC1 issuance, the Java saga was improperly commanding Compliance, Customer Identity, Billing, and Customer update steps. Fixed by publishing `PolicyIssuanceInitiatedEvent` and `AccountLookupRequestedEvent`, and by letting Billing and Customer Identity subscribe directly to `PolicyAdminSystemResponseReceivedEvent` fan-out.

- **Fan-out events must carry downstream data, not rely on saga state.** `PolicyAdminSystemResponseReceivedEvent` needed `accountServiceRequestNumber` so Billing and Customer Identity could complete their work independently without reaching back into Policy Issuance saga storage.

- **EDA logging is easier to trace when publisher/subscriber intent is explicit.** Added `[EDA publish]`, `[EDA subscriber]`, and `[EDA join]` log patterns across the affected Java routes so message ownership and fan-out behavior are visible in service logs.

### 2026-05-25 — Backend Code Quality Sweep

- **Domain isolation is solid.** All domain/*.java files (FileBatch, BatchRecord, IssuanceSagaRecord, ComplianceCheck) import only java.* — no Spring or MongoDB bleed. @Document/@Id annotations live exclusively in persistence/*.java documents. Pattern holds and should be enforced for all future entities.

- **MongoDB indexes live in Document classes, not domain classes.** @Indexed annotations belong on persistence/*Document.java fields. BatchRecordDocument was missing @Indexed on `batchId` and `correlationId` (both are query fields), IssuanceSagaDocument was missing @Indexed on `batchId`, ComplianceCheckDocument was missing @Indexed on `correlationId`. Fixed.

- **ProducerTemplate must be injected, never created inline.** `exchange.getContext().createProducerTemplate()` inside a processor creates a new non-managed ProducerTemplate per message — leaks resources and bypasses lifecycle management. Always inject ProducerTemplate as a Spring bean into the route class constructor. Fixed in RecordOutcomeRoute and FileArrivalRoute.

- **FileBatchRepository.findAll() was missing — compilation bug.** FileBatchController called fileBatchRepository.findAll(Sort...) against a domain interface that had no such method. Added findAll() to domain interface and implemented in adapter with Sort at the infrastructure layer. Domain interfaces must stay free of infrastructure types (no Sort parameters).

- **MDC context was absent in satellite services.** BillingAssociationRoute, ComplianceCheckRoute, and AccountServiceRoute had no MDC.put() calls, meaning issuanceId never appeared in their JSON log output. All fixed with MDC.put/clear bracketing each processor.

- **GlobalExceptionHandler was missing for both REST services.** Added @RestControllerAdvice in platform-file-processing-service and policy-issuance-service to return structured JSON errors for IllegalArgumentException (400), MethodArgumentNotValidException (400), and generic Exception (500).

- **POST /batches/generate returned 200 — should be 201.** File-creation endpoints that produce a new resource should return HTTP 201 Created. Fixed.

- **All events in common are already Java records.** No POJO-to-record conversion needed — every event class in common/events/** uses `public record`.

**Backend Code Quality Sweep Results:**
- 6 critical defects fixed: MongoDB indexes, ProducerTemplate leak, compilation bug, MDC gaps, missing exception handler, HTTP status
- All actively-queried fields now indexed (4 indexes across 3 document classes)
- ProducerTemplate injected as singleton bean in both high-throughput routes
- FileBatchController now compiles; findAll() infrastructure-clean in adapter
- MDC context now present in all 3 satellite service routes for distributed tracing
- GlobalExceptionHandler added to 2 REST services for consistent error responses
- HTTP 201 Created status now returned on resource creation
- No compilation errors; all tests pass
- Orchestration log: `.squad/orchestration-log/2026-05-26T01-33-25Z-backend-1.md`

