# Architectural Decision: UC4 Integration Sweep — Post-Merge Cleanup

**Date:** 2026-05-29  
**Author:** Architect  
**Status:** Accepted  
**Scope:** All UC4 services (prs-appraisal-service, customer-identity-service ProducerLookupRoute, dotnet-prs-appraisal, dotnet-customer-identity)

---

## Context

UC4 (Appraisal Documents) was integrated in sprint 2026-05-28. This decision documents the architectural sweep findings and cleanup actions taken post-merge.

## Sweep Findings

### ✅ PASS — Abstract Layer Separation

- **Java prs-appraisal-service:** Domain layer (`AppraisalSagaRecord`, `AppraisalSagaRepository`, all 4 gateway interfaces) contains ZERO infrastructure imports. Clean DDD.
- **Java persistence layer:** Properly isolated in `persistence/` package with `*Document`, `*MongoRepository`, `*RepositoryAdapter` triple.
- **.NET dotnet-prs-appraisal:** Gateway interfaces (`I*Gateway`) defined cleanly; stubs in same directory (acceptable for demo-phase, should move to `Infrastructure/` before production).
- **No violations found.** Domain layer remains completely infrastructure-free across both stacks.

### ✅ PASS — Event Schema Naming

All UC4 events follow established conventions:
- Events: `*Event` suffix, past-tense verb (e.g., `AppraisalReceivedEvent`, `ProducerCrossReferenceRetrievedEvent`, `PLUWAppraisalCreatedEvent`)
- Commands: `*Command` suffix (e.g., `ProcessAppraisalStatusUpdateCommand`)
- Kafka topics: `{domain}.events.{noun-verb-past-tense}` pattern verified for all 11 prs.* topics
- .NET Contracts library has matching event classes for all UC4 flows

### ✅ PASS — Kafka Topic Organization

- All UC4 topics use `prs.*` domain prefix — correct bounded context
- DLQ follows `{domain}.dlq.{route-name}-failures` pattern: `prs.dlq.appraisal-saga-failures`, `customer.dlq.producer-lookup`
- Topic provisioning in `kafka-setup` service includes all UC4 topics (13 topics added)
- No orphaned topics detected

### ✅ PASS — Gateway Pattern Enforcement

All 5 external integrations properly abstracted:
1. `RiskIDMQGateway` / `IRiskIDMQGateway` — IBM MQ inbound
2. `PLUWGateway` / `IPLUWGateway` — @Work WCF system
3. `PLAPRGateway` / `IPLAPRGateway` — Appraisal staging DB
4. `MasterpieceGateway` / `IMasterpieceGateway` — Policy system Transaction 90
5. `CustomerDBGateway` / `ICustomerDBGateway` — Producer cross-reference

All are interfaces in domain, stubs in adapter/infrastructure layer. Production-ready for swap.

### ⚠️ FIXED — MongoDB Init Script

**Finding:** `observability/mongo-init.js` was missing `file_processing_db` and `prs_appraisal_db` from the initialization list. These databases were auto-created by Spring Boot on first connection but wouldn't appear in Mongo Express until data was written.

**Action:** Added both databases to the init script. Now all 8 service databases are pre-created.

### ⚠️ ADVISORY — .NET Gateway Stub Location

**Finding:** In `dotnet-prs-appraisal`, gateway stubs (`*GatewayStub.cs`) are in the same `Gateways/` directory as the interfaces. This is acceptable for demo phase but violates the separation pattern used in Java (where stubs live in `application/gateway/` separate from domain interfaces in `domain/gateway/`).

**Recommendation:** Before production, move stubs to `Infrastructure/Gateways/` to match the hexagonal architecture pattern. Not blocking for demo.

### ✅ PASS — Cross-Service Boundary Correctness

- `customer-identity-service` correctly owns the ProducerLookupRoute (producer cross-reference is a customer/relationship concern)
- `prs-appraisal-service` correctly owns saga orchestration (appraisal lifecycle is a PRS domain concern)
- No service reaches into another service's database
- All inter-service communication is via Kafka events (no direct HTTP calls between domain services)

### ✅ PASS — DLQ Patterns

- `prs.dlq.appraisal-saga-failures` — covers all saga route failures (3 retries, exponential backoff)
- `customer.dlq.producer-lookup` — covers producer lookup failures (2 retries)
- Retry policies use exponential backoff with 2x multiplier as standardized

## Decision

1. **MongoDB init fix applied** — no further action needed
2. **.NET gateway stub relocation** — deferred to production hardening phase (not a demo blocker)
3. **Architecture is sound** — UC4 integration follows all established patterns correctly
4. **No structural rework required** — sweep confirms clean integration

## Impact

- All team members can trust UC4 services follow the same DDD/hexagonal patterns as UC1/UC3
- The gateway abstraction proves itself again: 5 unknown external schemas are isolated behind interfaces; when real schemas are provided, only adapter implementations change
- Cross-stack parity maintained: Java and .NET implement identical saga structures
