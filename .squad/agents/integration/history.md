# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Apache Camel (Java DSL + YAML DSL), Kafka, MongoDB, Azure, Docker
- **Architecture:** Event-driven pub/sub; all Camel components abstract-interfaced for stack portability; schema registry for Kafka events; DLQ on every consumer
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-26 — Integration Coverage Matrix Research

- **EDI not in scope**: No X12, EDIFACT, or AS2 found in any of the five in-scope Chubb BizTalk applications (SCI, PRS, ClaimCare, ECOS, Sanctions). The estate is XML/WCF/MQ-centric. This eliminates the hardest BizTalk replacement pattern from the migration scope.
- **IBM MQ is the most critical protocol dependency**: Confirmed in PRS (MQSC adapter — appraisal chain), Sanctions (IBM MQ polling — Clearance Processor), ClaimCare (MQSC in trigger inventory), and proposal explicitly lists IBM MQ as requiring ExpressRoute. Both Camel (JMS + IBM MQ JARs) and Logic Apps (IBM MQ connector + on-prem data gateway) need explicit setup.
- **MSMQ confirmed in SCI and ECOS**: Azure Service Bus is the canonical replacement. `camel-msmq` is Windows-only/community; this is a Camel gap. .NET/Azure wins on MSMQ replacement.
- **DB2 confirmed in ECOS and FiRM (via Genius ODBC)**: Logic Apps DB2 connector is well-supported; Camel uses JDBC. FiRM's ODBC path to Genius stored procedures may need custom code on either platform.
- **WCF-WSHTTP (WS-Security) confirmed in PRS**: Camel/CXF/WSS4J is more battle-tested for full WS-Security than CoreWCF (still maturing). Flag for Architect.
- **Camel-on-AKS hybrid**: The AIS+ architecture includes AKS as the primary hosting tier. A Camel component on AKS is architecturally coherent for IBM MQ flows or XSLT-heavy transforms within the .NET/Azure target platform — does not require choosing Camel as the primary platform.
- **Adapter inventory sources**: SCI POC doc, PRS POC doc, ECOS POC doc, Sanctions POC doc, AIS Proposal (section 3.2 "Integration Patterns Observed"), UC1/UC3 rebuild guides.
- **Output**: `.docs/intel-integration-coverage.md`

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

