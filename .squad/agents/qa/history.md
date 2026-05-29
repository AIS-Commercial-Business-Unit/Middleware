# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Integration tests against live docker-compose stack, Serilog/structured log verification, Gatling or k6 for load tests
- **Key principle:** Test evidence required for every feature (inputs + expected output + actual output + log excerpts); log verification asserts structured log entries fired with correct properties
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

### 2026-05-27 — Batch Processing Demo Investigation (Steven Suing)

**Problem:** Automated renewal batches start but never progress. Files are parsed and records dispatched, but processedRecords stays at 0 forever. Batches stuck in "Processing" state.

**Investigation Approach:**
1. Checked both Java and .NET file-processing services (both running in docker-compose)
2. Traced batch flow: file arrival → record dispatch → saga creation → saga completion → completion event → batch progress update
3. Found logs showing RenewalBatchRoute creating sagas but NO subsequent "Record succeeded" or "Batch progress" logs
4. Discovered error in policy-issuance-service logs: "The given id must not be null" in RenewalBatchRoute Route 2 (renewal-record-issued)
5. Inspected Kafka messages in policy.events.policy-issued topic

**Root Cause:** JSON serialization format mismatch between stacks.
- Java publishes PolicyIssuedEvent as: `{"issuanceId":"...", "accountServiceRequestNumber":"...", ...}` (camelCase)
- .NET publishes PolicyIssuedEvent as: `{"IssuanceId":"...", "AccountId":"...", ...}` (PascalCase)
- RenewalBatchRoute consumes both but deserializes with Jackson (expects camelCase)
- When deserializing .NET messages, `issuanceId` field is NULL → findById(null) exception → message DLQ'd
- RenewalRecordProcessedEvent never published → RecordOutcomeRoute never updates batch progress → batch stays at 0%

**Stack Architecture:** Both Java and .NET stacks are fully operational and process batches simultaneously. Cross-stack message compatibility broken by serialization format divergence.

**Test Evidence:**
- Batch API shows 4 batches all stuck in "Processing" with totalRecords=10 but processedRecords=0
- Kafka topic inspection confirms two message formats in single topic
- Error logs contain `java.lang.IllegalArgumentException: The given id must not be null` from IssuanceSagaRepositoryAdapter.findById(null)
- No "Record succeeded" or "Batch progress" entries in platform-file-processing-service logs despite saga creation logs in policy-issuance-service

**Recommendation:** Standardize JSON field naming across stacks. Add JsonProperty attributes to .NET event classes to use camelCase, matching Java/Jackson conventions. This ensures when cross-stack messages are serialized to Kafka and consumed by different stacks, deserialization succeeds and fields populate correctly.

### 2026-05-28 — UC4 Appraisal Documents Test Scenarios + Demo Script

**Task:** Create test scenarios, demo script, log verification checklist, integration test plan, and demo gap documentation for UC4 Appraisal Documents.

**Key files produced:**
- `.docs/uc4-test-scenarios.md` — 8 test scenarios (SC-001 through SC-007 + 6 GAP scenarios)
- `.docs/uc4-demo-script.md` — Full demo script with pre-setup, step-by-step walkthrough, and prep session questions

**Architecture facts confirmed:**
- Dual-stack: Java (Apache Camel + MongoDB) and .NET (NServiceBus + MongoDB) — Decision #15 confirms MongoDB for saga persistence in both stacks, SQL Server is NServiceBus transport only
- EDA_FLOW structured logging required on every Kafka hop — same property contract (`EDA_Event`, `EDA_IssuanceId`, `EDA_MessageType`, `EDA_From`, `EDA_To`, `EDA_Topic`, `EDA_Direction`, `EDA_Stack`) in both Java and .NET stacks
- DLQ topic naming: `appraisal.dlq.status-update-failures` (follows Decision #2: `{domain}.dlq.{route-name}`)
- Retry policy: 3 attempts with exponential backoff before DLQ (Decision #2)
- Gateway stubs must log `⚠️ STUBBED` markers — this is a demo contract, not just a nice-to-have

**Demo gaps identified (6 high/medium risk):**
1. PLAPR table schema and stored procedure fields (🔴 HIGH)
2. @Work IBM MQ message format (🔴 HIGH)
3. UW determination rule codes — UA vs UST routing logic (🔴 HIGH)
4. Masterpiece Transaction 90 (PLIPQP90) payload format (🔴 HIGH)
5. Other active RiskID StatusCodes beyond 6 and 15 (🟡 MEDIUM)
6. Real IBM MQ message schema from RiskID (🔴 HIGH)

**Pattern learned:** For BizTalk replacement demos, always distinguish between "architecture proven" and "business rules validated." The saga pattern, gateway abstraction, and EDA_FLOW observability are testable without real system access. Business rule correctness (routing logic, message formats, field mappings) requires domain expert input. Document these separately so stakeholders understand what is and isn't confirmed.

**Test scenario IDs assigned:**
- SC-001: Happy Path UA (INS-001, code=6, type=A)
- SC-002: Happy Path UST (INS-002, code=6, type=B)
- SC-003: Inspection type I (INS-003, code=6, type=I)
- SC-004: Inspection type J (INS-003J, code=6, type=J) — fully unspecified
- SC-005: Completed flow (INS-005, code=15, type=A)
- SC-006: Timeout → DLQ (INS-004, code=6, type=A)
- SC-007: Gateway hard failure → DLQ (INS-007, code=6, type=A)

**Kafka topics used by UC4:**
- `appraisal.command.status-update-received`
- `customer.query.producer-lookup-requested`
- `customer.event.producer-crossref-retrieved`
- `appraisal.command.pluw-appraisal-create-requested`
- `appraisal.command.uw-determination-requested`
- `appraisal.event.appraisal-uw-assigned`
- `appraisal.event.appraisal-completed`
- `appraisal.event.appraisal-status-update-failed`
- `appraisal.dlq.status-update-failures`

**MongoDB collections for UC4:**
- `appraisal_received_sagas`
- `appraisal_statuscode6uw_sagas`
- `plapr_staging` (stub)
- `gateway_call_log`

### 2026-05-27 — Cross-agent synchronization (Scribe session)

- **Root cause diagnosis validated:** DotNet team confirmed QA's cross-stack serialization hypothesis and implemented Option A (centralized `JsonNamingPolicy.CamelCase` in `KafkaBridgeRuntime`). Batch processing demo now completes end-to-end with `processedRecords: 3` for a 3-record test batch.

- **dotnet-3 infrastructure pattern discovered:** While QA investigated batches, dotnet-3 uncovered complementary infrastructure-critical pattern — NServiceBus container startup sequencing. `dotnet-platform-integration` startup is prerequisite for queue table creation. If container doesn't start, subscribed messages cannot be routed.

- **frontend-3 visualization confirmed architecture:** Flow diagram fixes (dedup key, mappings) combined with dotnet-4 serialization fix enabled frontend to display live cross-stack event flows. Operators can now verify batch/issuance processing topology in real time from Loki events.

- **Decisions archive:** 4 inbox files merged into unified `.squad/decisions/decisions.md`. Serialization, startup, and observability conventions now central reference for team-wide cross-stack validation.

### 2026-05-29 — UC4 Smoke Test + Demo Gap Verification

**Task:** Start UC4 containers, verify DEMO GAP markers, execute SC-001 smoke test end-to-end.

**Services involved (all required rebuilds — stale containers from before UC4 code was added):**
- `platform-integration-service` — rebuilt (RiskIDGatewayRoute + RiskIDMQGateway newly added)
- `prs-appraisal-service` — rebuilt twice (see bugs below)
- `customer-identity-service` — rebuilt (ProducerLookupRoute newly added)
- `dotnet-prs-appraisal` — started manually after crash-loop on initial compose up

**Bugs found and fixed:**
1. **prs-appraisal-service missing MongoConfig** — `OffsetDateTime` fields in `AppraisalSagaDocument` had no codec registered. Error: `Can't find a codec for CodecCacheKey{clazz=class java.time.OffsetDateTime}`. Fix: added `config/MongoConfig.java` (identical to `policy-issuance-service/config/MongoConfig.java` — same pattern, same converters).
2. **Duplicate `status` criteria in CAS query** — `AppraisalReceivedSagaRoute.java:298-299` used `.and("status").ne("UpdatingDownstream").and("status").ne("Completed")` which Spring Data MongoDB rejects. Fix: changed to `.and("status").nin("UpdatingDownstream", "Completed")`.

**Smoke test SC-001 result (APR-SC001-FINAL, POL-12345, statusCode=6, typeCode=A):**
- HTTP 202 from platform-integration-service ✅
- Kafka: `prs.events.appraisal-received` message published ✅
- AppraisalReceivedSaga picked up and created in MongoDB ✅
- ProducerLookupRequestedEvent → CustomerIdentity ✅ (STUBBED + DEMO GAP logged at WARN)
- StatusCode6UWSaga parallel arms (PLUW + UW determination) ✅
- All 5 stub gateways logged `⚠️ STUBBED` and `⚠️ DEMO GAP` at WARN level ✅
- Join complete: uwAssignment=UA, suspenseDays=45 ✅
- `AppraisalUnderwriterAssignedEvent` published to `prs.events.appraisal-uw-assigned` ✅
- MongoDB saga: status=Completed, producerCode=REPLACE_ME_PROD001, pluwReferenceId=PLUW-STUB-F991EB58 ✅
- saga-timeout-watchdog route running ✅; timeoutAt = receivedAt + 60min in MongoDB ✅

**Demo gap visibility confirmed (runtime logs):**
- 9 `⚠️ DEMO GAP` log entries per SC-001 run (6 prs-appraisal + 3 customer-identity)
- 7 `⚠️ STUBBED` log entries per run
- 17 REPLACE_ME_ in Java source, 63 in C# source
- All stub logs at WARN level — visible in log aggregators

**Key file paths:**
- New file: `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/config/MongoConfig.java`
- Fixed: `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/routes/AppraisalReceivedSagaRoute.java` line ~298
- UC4 gateway stubs: `java/prs-appraisal-service/src/main/java/com/ais/middleware/prs/appraisal/application/gateway/Stub*.java`
- UC4 .NET stubs: `dotnet/dotnet-prs-appraisal/Gateways/*Stub.cs`

**Pattern: MongoConfig required in every service that uses OffsetDateTime in a MongoDB document.** prs-appraisal-service was the only service missing it. All other services with MongoDB persistence already have it.

**dotnet-prs-appraisal startup note:** Container exited 139 (segfault) repeatedly on initial `docker compose up`. Resolved by `docker start dotnet-prs-appraisal` after other containers stabilized. Suspected Rancher Desktop memory pressure during simultaneous rebuild of 3 services. Monitor before demo.

### Cross-agent learning — MongoConfig + Criteria .nin() pattern (2026-05-29)

**From Scribe decisions merge:**

1. **MongoConfig Required for Every Service Using OffsetDateTime in MongoDB** — Spring Data MongoDB 4.x does not natively support `java.time.OffsetDateTime` without custom codec. Every Java service that uses Spring Data MongoDB AND has `OffsetDateTime` fields in domain/persistence classes MUST include `MongoConfig.java`. Copy canonical template from `policy-issuance-service/config/MongoConfig.java`. Converters: `OffsetDateTimeToDateConverter` (OffsetDateTime → BSON Date UTC) and `DateToOffsetDateTimeConverter` (BSON Date → OffsetDateTime UTC). prs-appraisal-service was the only missing instance in current codebase.

2. **Use `.nin()` for Multi-Value Exclusion in MongoDB Criteria** — Chained `.ne()` calls on same field throw `InvalidMongoDbApiUsageException`. When excluding multiple values from same field, use `.nin(val1, val2, ...)` NOT chained `.ne()` calls. Example: `Criteria.where("_id").is(id).and("status").nin("A", "B")` ✅ not `and("status").ne("A").and("status").ne("B")` ❌. Applied to `findAndModify`, `find`, and `update` queries.

**Pattern established:** Both decisions codify patterns discovered during UC4 smoke test — verified against prs-appraisal-service and propagated as team-wide rules for consistency.
