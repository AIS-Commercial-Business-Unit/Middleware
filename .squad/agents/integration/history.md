# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel (Java DSL + YAML DSL), Kafka, MongoDB, Azure, Docker
- **Architecture:** Event-driven pub/sub; all Camel components abstract-interfaced for stack portability; schema registry for Kafka events; DLQ on every consumer
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-25 — UC3 AutomatedRenewal Batch Backend

- **File polling pattern**: Camel file component with `@Value`-injected path variables is the correct approach in Java DSL (not Camel `{{...}}` placeholders which require Camel property component to be configured). URI built by string concatenation in `configure()` after field injection.
- **CSV parsing**: OpenCSV `CSVReader` with `StringReader` works cleanly inside a Camel processor when the file body is read as `String`. Charset must be declared in the file endpoint URI (`&charset=UTF-8`).
- **Atomic MongoDB counters**: Use `MongoTemplate` with `$inc` operator rather than read-modify-write for batch progress counters. This prevents race conditions when multiple `RenewalRecordProcessed`/`Failed` events arrive concurrently.
- **Kafka group ID isolation**: When a new route subscribes to a topic that already has consumers (e.g. `policy.events.policy-issued`), use a *distinct* group ID so both consumer groups each get a full copy of every message. Using the same group ID would round-robin messages between route instances.
- **ProducerTemplate lifecycle**: Calling `exchange.getContext().createProducerTemplate()` in a processor creates a new template instance each call. Acceptable for low-frequency paths; for high-throughput routes, inject a shared `ProducerTemplate` bean instead.
- **BatchRecord correlation key**: The `correlationId` field on `BatchRecord` == the `issuanceId` assigned at record dispatch time. This is the lookup key when `RenewalRecordProcessed`/`Failed` events return from policy-issuance-service.
- **Idempotency in RenewalBatchRoute**: `repository.existsById(issuanceId)` is sufficient for idempotency because `correlationId` (= `issuanceId`) is generated once per `BatchRecord` at dispatch and never reused.

### 2026-05-25 — Integration Architecture Sweep

- **DLQ pattern for Kafka consumers**: Always use `onException(Exception.class).maximumRedeliveries(2).redeliveryDelay(1000).backOffMultiplier(2).useExponentialBackOff().handled(true).to("kafka:{domain}.dlq.{route}")`. `handled(true)` is mandatory — without it, Camel does not commit the Kafka offset and the message redelivers forever.
- **DLQ pattern for file consumers**: Do NOT use `handled(true)` on `onException` in file polling routes. Without `handled(true)`, the exception propagates to the file component which moves the file to `moveFailed` (errorDir). With `handled(true)`, failed files are incorrectly moved to `move` (processedDir) and silently lost.
- **checkJoinCondition race condition**: The original read-modify-write for saga join flags (`billingComplete`/`customerUpdateComplete`) was a classic TOCTOU race. Two concurrent events each read stale state, each write only their own flag, and neither sees both flags set → `PolicyIssued` is never published. Fix: use `MongoTemplate.findAndModify` with `returnNew(true)` for the flag set, then a conditional CAS (`status != Completed`) for the final publish. Exactly one thread wins the CAS and publishes.
- **`findAndModify` with `IssuanceSagaDocument`**: The route directly uses `IssuanceSagaDocument` (persistence layer class) with `MongoTemplate` because the domain repository interface (`IssuanceSagaRepository`) doesn't expose atomic operations. This is an acceptable controlled breach of layering for performance-critical atomic ops.
- **`fileprocessing` domain name**: The canonical domain segment for `platform-file-processing-service` topics is `file` (not `fileprocessing`). Consistent with all other single-word domain names. Topics: `file.events.*`, `file.dlq.*`.
- **`maxMessagesPerPoll` on file routes**: Always set `&maxMessagesPerPoll=N` on file endpoints in production. Without it, a burst of queued files (e.g., after outage) processes all files in one poll cycle, which can exhaust heap on large CSV files. `10` is a reasonable default for renewal batch.
- **DLQ topic naming standard**: `{domain}.dlq.{route-name}` — no `-failures` suffix. Maps 1:1 to the route that publishes to it for easy ops correlation.

**Integration Architecture Sweep Results:**
- 4 critical issues fixed: race condition, DLQ inconsistencies, topic naming, file polling
- All 9 routes now have consistent DLQ handlers with exponential backoff
- All fileprocessing topics renamed to file.events.*
- Atomic join condition prevents duplicate PolicyIssued events
- UC1 & UC3 verified end-to-end with no race conditions
- Orchestration log: `.squad/orchestration-log/2026-05-26T01-33-25Z-integration-1.md`

