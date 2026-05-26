# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Java (Spring Boot or Quarkus), MongoDB, REST APIs, OpenAPI specs, SLF4J structured logging
- **Key principle:** Repository interfaces in domain layer; MongoDB implementation in infrastructure layer; domain never imports infrastructure types; correlation IDs on all logs
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-25 — Backend Code Quality Sweep

- **Domain isolation is solid.** All domain/*.java files (FileBatch, BatchRecord, IssuanceSagaRecord, ComplianceCheck) import only java.* — no Spring or MongoDB bleed. @Document/@Id annotations live exclusively in persistence/*.java documents. Pattern holds and should be enforced for all future entities.

- **MongoDB indexes live in Document classes, not domain classes.** @Indexed annotations belong on persistence/*Document.java fields. BatchRecordDocument was missing @Indexed on `batchId` and `correlationId` (both are query fields), IssuanceSagaDocument was missing @Indexed on `batchId`, ComplianceCheckDocument was missing @Indexed on `correlationId`. Fixed.

- **ProducerTemplate must be injected, never created inline.** `exchange.getContext().createProducerTemplate()` inside a processor creates a new non-managed ProducerTemplate per message — leaks resources and bypasses lifecycle management. Always inject ProducerTemplate as a Spring bean into the route class constructor. Fixed in RecordOutcomeRoute and FileArrivalRoute.

- **FileBatchRepository.findAll() was missing — compilation bug.** FileBatchController called fileBatchRepository.findAll(Sort...) against a domain interface that had no such method. Added findAll() to domain interface and implemented in adapter with Sort at the infrastructure layer. Domain interfaces must stay free of infrastructure types (no Sort parameters).

- **MDC context was absent in satellite services.** BillingAssociationRoute, ComplianceCheckRoute, and AccountServiceRoute had no MDC.put() calls, meaning issuanceId never appeared in their JSON log output. All fixed with MDC.put/clear bracketing each processor.

- **GlobalExceptionHandler was missing for both REST services.** Added @RestControllerAdvice in platform-file-processing-service and policy-issuance-service to return structured JSON errors for IllegalArgumentException (400), MethodArgumentNotValidException (400), and generic Exception (500).

- **POST /batches/generate returned 200 — should be 201.** File-creation endpoints that produce a new resource should return HTTP 201 Created. Fixed.

- **All events in common are already Java records.** No POJO-to-record conversion needed — every event class in common/events/** uses `public record`.

