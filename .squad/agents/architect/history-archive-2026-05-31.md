# Architect History Archive — Before 2026-05-31

## Detailed Learnings (Archived)

### 2026-05-31: EDA Compliance Review — UC4 Scatter-Gather Pattern

**Finding:** The UC4 `DocumentListSaga` scatter-gather is architecturally incomplete. The AtWork path is synchronous inline (temporal coupling) while the mainframe path is correctly event-driven. The requirements define a published-event fan-out pattern (§9.2, §10) where `AppraisalDocumentListRequested` triggers independent handlers — this is not implemented. The event contracts exist (`Uc4AppraisalDocumentListRequestedEvent`, `Uc4AtWorkDocumentListCompletedEvent`) but are dead code.

**Key Principle Reinforced:** In NServiceBus scatter-gather, the coordinator saga must ONLY publish an event and wait for completion events. It must NEVER call services inline or send direct commands to known participants. This is the Udi Dahan separation: the saga doesn't know who will respond, only that responses will arrive.

**Action:** Decision inbox entry filed (`architect-eda-review-uc4.md`) with 3 critical, 3 important, and 3 minor findings. Primary fix is a single refactor pass: publish event → create AtWork handler → convert mainframe aggregator from command to event subscription → saga waits for both completion events.

### 2026-05-29: UC4 Architecture Sweep & Decisions Finalization

**UC4 Architecture Audit (Post-Merge Cleanup):**
- All UC4 services (Java prs-appraisal-service, customer-identity-service, .NET dotnet-prs-appraisal, dotnet-customer-identity) pass domain/infrastructure separation audit
- Domain layer contains ZERO infrastructure imports; persistence adapters properly isolated in `persistence/` or `Infrastructure/` packages
- All 5 gateway interfaces (`RiskIDMQGateway`, `PLUWGateway`, `PLAPRGateway`, `MasterpieceGateway`, `CustomerDBGateway`) cleanly abstracted; stubs in adapter layer
- Event schema naming verified across all 11 UC4 prs.* topics; kafka-setup pre-creates all topics with correct DLQ pattern (`prs.dlq.{route-name}`)
- MongoDB init script was missing `file_processing_db` and `prs_appraisal_db` — fixed
- Cross-service boundary correctness verified: customer-identity owns ProducerLookupRoute, prs-appraisal owns saga orchestration, no cross-database access
- DLQ patterns verified: `prs.dlq.appraisal-saga-failures` (3 retries), `customer.dlq.producer-lookup` (2 retries)

### 2026-05-27: EDA Events vs Commands — Udi Dahan Principles

**Directive from Steven Suing**: Udi Dahan is the authoritative source for EDA architectural guidance on this project. Consult his work for any event-driven architecture decisions.

**Core rule (from NServiceBus docs + Udi Dahan)**:
- Commands: Tell ONE service to do something. Have one logical owner. Are SENT directly. Cannot be published. Cannot have multiple handlers.
- Events: Communicate that something happened. Have one logical PUBLISHER. Are PUBLISHED. Any service can SUBSCRIBE. Never sent directly.

**Fan-out pattern (pub/sub)**:
Platform.Integration publishes `PolicyAdminSystemResponseReceived` ONCE. THREE services subscribe independently:
1. PolicyIssuanceAndLifecycleManagement — updates issuance state
2. BillingAndFinanceManagement — associates billing account → publishes `BillingAssociationCreatedEvent`
3. CustomerIdentityAndRelationshipManagement — links customer to policy → publishes `CustomerUpdatedEvent`
PolicyLifecycle waits for both completion events (join pattern) before publishing `PolicyIssuedEvent`.

**EDA Observability Contract Across Stacks:**
- Java: `AppraisalReceivedSagaRoute` uses existing `EDAFlowProcessor` intercepts; `appraisalId` mapped to `correlationId` fallback path
- .NET: Planned `EDAFlowBehavior` (NServiceBus pipeline) will emit same `EDA_*` MDC contract as Java
- Correlation key: `appraisalId` (UC4) stored as `correlationId` property/header so both observability systems pick it up without code duplication

---

This archive documents foundational work on UC4 architecture, EDA compliance, and cross-stack parity established 2026-05-27 through 2026-05-29. See main `history.md` for recent session learnings.
