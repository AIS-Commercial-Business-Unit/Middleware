# Architect Agent — History Archive

Older learnings archived from history.md on 2026-05-29.

## Archived Entries

### 2026-05-26 — EIP Pattern Checklist Closes Technology Debate

**Positioning Guidance:**
- Once platform demonstrates all major BizTalk-replacement EIP patterns (dead letter queue, retry, pub/sub, scatter-gather, saga, idempotent consumer, outbox, content-based routing, message translation, data movement/ETL adapter flows), the technical viability argument is closed.
- Remaining delivery risk must be framed as organizational: program management, migration sequencing, CI/CD maturity, team enablement, and governance.
- This repo is proof that patterns work; professional services is the mechanism for migrating 67+ real applications without collapse.
- CI/CD must be a prerequisite for integration modernization, not a follow-on implementation detail.

**AIS Stack Framing Correction:**
- Azure APIM, Blob Storage, Key Vault, App Configuration, Entra ID, Monitor, App Insights are shared platform services, NOT stack differentiators.
- With Kafka as the production backbone, Java + Camel removes Azure Service Bus and Logic Apps entirely.
- .NET + NServiceBus typically adds Azure Service Bus or SQL Server as message infrastructure; Logic Apps bridges protocol gaps Camel handles natively.
- The Java/Camel path is architecturally simpler because it preserves shared Azure services while removing operational sprawl.

### 2026-05-25 — Architecture Sweep

**DDD Layer Separation Pattern:**
- Domain entities MUST be clean Java — no Spring, MongoDB, or Camel annotations
- Infrastructure lives in `persistence/` subpackage with `*Document.java`, `*MongoRepository.java`, `*RepositoryAdapter.java`
- Domain repositories are pure Java interfaces

**DLQ Convention:**
- Every Camel route must have `onException` handler with DLQ
- Topic pattern: `{domain}.dlq.{route-name}-failures`
- Standard policy: 3 retries, exponential backoff, then DLQ

**Naming Conventions & Coordination Results:**
- Commands: `*Command` suffix
- Events: `*Event` suffix
- UC1 and UC3 verified end-to-end after sweep
- All DDD violations eliminated

### 2026-05-26 — Java vs .NET BizTalk Replacement Analysis

**Key Decision Rationale:**
- Apache Camel provides native components for every adapter pattern in client BizTalk environment
- Client team already operates Java, Kafka, MongoDB, Docker — zero retraining required
- .NET/NServiceBus would require Logic Apps for adapter gaps
- Program management value outweighs platform adoption considerations
- **Output:** Java path is architecturally simpler, operationally lighter

### 2026-05-28 — UC4 Appraisal Documents Architecture & Demo Gap Analysis

**Architecture & Cross-Stack Alignment:**
- UC4 Appraisal service follows identical DDD/gateway patterns as UC1
- Gateway pattern enforcement validated across Java and .NET
- Saga structure: AppraisalReceivedSaga (outer) → StatusCode6UWSaga, StatusCode15CompletedSaga, GenericStatusUpdateSaga
- Timeout handling: Java uses timer + MongoDB check; NServiceBus uses built-in RequestTimeout
- Join pattern for parallel calls uses MongoDB `findAndModify()` atomic check
- All 6 agents completed UC4 in parallel with zero blocking dependencies
- Demo gap visibility standard: `⚠️ STUBBED` and `⚠️ DEMO GAP` markers in logs
- QA two-section test standard (architecture patterns + demo gaps)
