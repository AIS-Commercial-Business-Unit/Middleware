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

### 2026-05-27 — Cross-agent synchronization (Scribe session)

- **Root cause diagnosis validated:** DotNet team confirmed QA's cross-stack serialization hypothesis and implemented Option A (centralized `JsonNamingPolicy.CamelCase` in `KafkaBridgeRuntime`). Batch processing demo now completes end-to-end with `processedRecords: 3` for a 3-record test batch.

- **dotnet-3 infrastructure pattern discovered:** While QA investigated batches, dotnet-3 uncovered complementary infrastructure-critical pattern — NServiceBus container startup sequencing. `dotnet-platform-integration` startup is prerequisite for queue table creation. If container doesn't start, subscribed messages cannot be routed.

- **frontend-3 visualization confirmed architecture:** Flow diagram fixes (dedup key, mappings) combined with dotnet-4 serialization fix enabled frontend to display live cross-stack event flows. Operators can now verify batch/issuance processing topology in real time from Loki events.

- **Decisions archive:** 4 inbox files merged into unified `.squad/decisions/decisions.md`. Serialization, startup, and observability conventions now central reference for team-wide cross-stack validation.
