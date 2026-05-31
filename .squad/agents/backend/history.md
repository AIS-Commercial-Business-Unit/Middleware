# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Java (Spring Boot or Quarkus), MongoDB, REST APIs, OpenAPI specs, SLF4J structured logging
- **Key principle:** Repository interfaces in domain layer; MongoDB implementation in infrastructure layer; domain never imports infrastructure types; correlation IDs on all logs
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-30 — DEIPDE07 MQ Simulator stub built

- **Spring Boot Artemis autoconfiguration is sufficient for JMS stubs.** `spring-boot-starter-artemis` with `spring.artemis.mode=native` provides both `ConnectionFactory` and `JmsTemplate` beans automatically. No manual `@Bean` wiring needed; `JmsConfig` is a documentation placeholder only.

- **`@JmsListener` supports `Session` as a method parameter directly.** Spring JMS injects the JMS `Session` into `@JmsListener` methods so the simulator can use it alongside the injected `JmsTemplate` for sends — no need for `SessionAwareMessageListener`.

- **JMS stub listener must never rethrow exceptions.** Rethrowing from a `@JmsListener` method triggers Spring JMS to roll back / retry the message. For simulator services that must stay alive, catch all exceptions, log with correlationId, and return normally.

- **`JmsTemplate.send(queue, MessageCreator)` is the correct pattern for reply-correlation.** Using `template.send(responseQueue, session -> { msg.setJMSCorrelationID(correlationId); return msg; })` inside a lambda ensures the correlationId is always set on each outbound message.

- **Jakarta EE namespace (`jakarta.jms.*`) required for Spring Boot 3.x.** The task spec listed `javax.jms.*` but this codebase uses Spring Boot 3.3.4 which migrated to Jakarta EE 9. All JMS imports use `jakarta.jms`.

### 2026-05-29 — Gateway latency + Demo Reset API + Decisions Merged (backend-1 complete)


### 2026-05-28 — UC4 prs-appraisal-service built (backend-5 complete)

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


### 2026-05-30 — DEIPDE07 MQ Simulator (UC4 build complete)

- **Multi-message fixture responses require SEQUENCE=X OF N tracking.** The simulator's AppraisalListResponder sends N separate JMS messages for a single appraisal list result; each message includes SEQUENCE=X and TOTAL_MESSAGES=N headers. The consuming route must correlate by JMS correlation ID and buffer until all N messages arrive.

- **64-byte chunks with CRLF EBCDIC artifacts require detection and stripping.** DocumentChunkResponder sends PDF bytes as 64-byte chunks with \r\n line terminators (EBCDIC legacy). The aggregation route must detect the ||END-OF-DOCUMENT|| sentinel and strip all \r\n before base64-encoding the final PDF.

- **Configurable delays and queue names via environment variables enable test flexibility.** The simulator exposes ${mq.request.queue}, ${mq.response.queue}, ${mq.poll.timeout.seconds}, and response delays as @Value fields. docker-compose can override these to simulate different failure modes (long delays, queue name mismatches).

### 2026-05-31 — UC4 Postman collection and demo guide generated

- **Postman v2.1 collection JSON structure enables parameterized request URLs and per-request timeouts.** The UC4 collection uses `{{baseUrl}}` variable (http://localhost:8090) and the timeout scenario request overrides global timeout with `"timeout": 40000` (ms). Each request stores a detailed `description` field so demo observers understand what to look for in Artemis console and logs.

- **Demo guide structure pairs infrastructure visibility (Artemis queues, Grafana traces) with step-by-step user actions.** The 5-minute script walks through happy path (3 records), edge cases (zero results), chunked documents (8 and 200 chunks), timeout resilience, and alternate integration paths (RiskID WCF), with real-time queue observations at each step to reinforce scatter-gather and MQ mechanics.

## 2026-05-31 — UC4 Dotnet Rewrite Completed (backend-2)
- Replaced the legacy AppraisalReceivedSaga workflow in dotnet/dotnet-prs-appraisal with UC4 document list and document retrieval sagas.
- Added the UC4 REST facade, callback registry, Artemis MQ adapter/listeners, and new Middleware.Contracts commands/events/models.
- Updated Program.cs and appsettings.json for the UC4 NServiceBus + Artemis wiring and verified dotnet build dotnet\\dotnet-prs-appraisal\\dotnet-prs-appraisal.csproj -nologo succeeds.
- **Key learning (2026-05-31):** .NET TaskCompletionSource callback registry bridges async NServiceBus sagas to sync HTTP facades. When DocumentListSaga completes (all Artemis replies received), it fires a completion event that the callback handler matches via correlation ID and resolves the waiting Task. This keeps the HTTP response synchronous while preserving asynchronous MQ integration — critical for UC4 because HTTP clients (browser, Postman) expect response payload, not streaming/webhooks.
