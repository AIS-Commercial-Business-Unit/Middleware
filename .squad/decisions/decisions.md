# Decision Log

---

## Decision: Java/Apache Camel Stack for BizTalk Replacement

**Decision ID:** architect-java-vs-dotnet-recommendation  
**Status:** Proposed  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing  

---

### Context

Client is modernizing 67+ BizTalk applications across four business portfolios (SCI: 18, PRS/RiskID: 19, ClaimCare: 30, ECOS: 4+). The client's existing operational team runs Java, Kafka, MongoDB, SQL Server, and Docker. The question was whether to build the replacement on Java/Apache Camel or .NET/NServiceBus.

### Decision

**Java/Apache Camel** is the recommended stack. Unambiguous. No conditions.

### Rationale

1. **Adapter coverage:** Every BizTalk adapter in use (IBM MQ, SFTP, File, WCF/SOAP, SQL polling, DB2) has a mature Camel component. .NET would require Logic Apps or custom code for the same coverage.
2. **Operational fit:** The team already runs Java/Kafka/MongoDB. Adopting .NET requires full team retraining.
3. **Licensing:** Camel is Apache 2.0 (free). NServiceBus is commercially licensed per-endpoint.
4. **One operational model:** Camel as Spring Boot containers on Kafka. No Logic Apps sprawl. No split tooling.
5. **The saga gap is manageable:** NServiceBus has better saga primitives, but sagas are implementable on Camel/Kafka with the architecture already designed.

### Consequences

- Professional services engagement focuses on program execution (discovery, architecture, migration, testing, change management) rather than technology platform adoption
- AIS delivers integration architecture expertise, not .NET retraining
- Estimated 12-18 month program duration
- Zero framework licensing cost

### Alternatives Considered

- **.NET/NServiceBus:** Superior saga support but requires team retraining, commercial licensing, Logic Apps for adapter gaps, and introduces operational complexity
- **Hybrid:** Rejected — maintaining two stacks doubles operational burden

### Supporting Document

Full analysis: `.docs/java-vs-dotnet-biztalk-replacement.md`

---

## Decision: AIS Stack Framing for BizTalk Replacement

**Decision ID:** architect-ais-stack-framing  
**Status:** Adopted  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing

---

### Context

Architecture review of Azure Integration Services as a differentiator in technology selection. Azure services (APIM, Blob Storage, Key Vault, App Config, Entra ID, Monitor, App Insights) are shared across both Java/Camel and .NET/NServiceBus stacks.

### Decision

Frame Azure Integration Services as a **shared platform baseline plus .NET-specific additions**, not as an inherent reason to choose .NET.

### Shared by Both Stacks

- Azure API Management
- Azure Blob Storage
- Azure Key Vault
- Azure App Configuration
- Microsoft Entra ID
- Azure Monitor / Application Insights

These are not differentiators. Both Java/Camel and .NET/NServiceBus can use them.

### Differentiating Stack Impact

#### Java + Kafka + Camel
- Reuses Kafka as the message bus
- Keeps integration logic in containerized services
- Eliminates Azure Service Bus
- Eliminates Azure Logic Apps

#### .NET + NServiceBus
- Keeps Kafka for event streaming because it already exists in production
- Typically adds Azure Service Bus for NServiceBus transport in Azure
- Or adds SQL Server as message infrastructure if avoiding Service Bus
- Adds Logic Apps for protocol adapters Camel handles natively (SFTP, IBM MQ bridging, file polling, SQL polling, AS2)

### Rationale

If the client already has Kafka, the Azure Integration Services pitch becomes an argument against the .NET path: it introduces a second message bus and a second integration runtime instead of simplifying the estate. Java/Camel is the smaller target architecture because it preserves the shared Azure platform services while removing Azure Service Bus and Logic Apps from the solution.

---

## Decision: Pattern Checklist Closes the Technology Debate

**Decision ID:** architect-patterns-org-argument  
**Status:** Adopted  
**Date:** 2026-05-26  
**Author:** Architect  
**Requested by:** Steven Suing

---

### Context

Clarifying the scope of the technical platform decision. Once a platform demonstrates the major Enterprise Integration Patterns required by the BizTalk estate, the technical viability is proven.

### Decision

Position the BizTalk modernization argument around a hard line: once the target platform demonstrates the major Enterprise Integration Patterns required by the estate, the technical debate is over and the remaining risk is organizational.

### Pattern Checklist (Proof of Capability)

The platform must prove, end-to-end, the patterns that matter for BizTalk replacement:

- Dead letter queue
- Retry / redelivery
- Publish / subscribe
- Scatter-gather
- Saga / long-running process
- Idempotent consumer
- Outbox
- Content-based routing
- Message translation
- Data movement / ETL-style adapter flows

In this repository, that proof point is already established across the Java/Camel and .NET/NServiceBus demonstrations.

### Consequences

If the checklist is green, the client does not have a technology selection problem. The client has a delivery problem:

- Sequencing 67+ applications
- Running discovery deeply enough to uncover real customizations
- Standing up CI/CD before the first migration ships
- Training teams on the new operational model
- Governing domain boundaries across portfolios

### Services Framing

The winning professional services message is not "our stack is better than BizTalk." That is expected. The winning message is that AIS has the methodology, architecture patterns, and program discipline to migrate a large BizTalk estate without the effort collapsing under dependency chaos, weak testing, and manual deployment.

### Operational Requirement

CI/CD is a prerequisite, not a follow-up task. If the target organization cannot build, test, promote, and observe integration services independently, the first successful migration will become the first manually operated liability.

---

## Directive: Organizational Problem vs. Technical Problem

**Directive ID:** copilot-organizational-framing  
**Status:** Captured  
**Date:** 2026-05-26  
**Author:** Steven Suing (via Copilot)  
**Source:** User directive

---

### Context

Clarification of the core winning argument for professional services engagement.

### Directive

If all major EIP patterns are accounted for in the platform (dead letter queue, retry, pub/sub, scatter-gather, saga, idempotent messages, outbox, translation/data movement), then the client's problem is NOT a technical problem. It is an organizational and process problem — program management, strategy, good communication, team organization, and CI/CD investment.

### Rationale

This is the core winning argument for professional services. The platform proof establishes viability; AIS's value is in delivering program discipline, architecture governance, and operational enablement across 67+ applications without the effort collapsing.

---

## Decision: .NET Kafka Events Must Use camelCase JSON Serialization

**Decision ID:** dotnet-kafka-camelcase  
**Status:** Implemented  
**Date:** 2026-05-27  
**Author:** DotNet agent  
**Requested by:** Steven Suing

---

### Context

.NET services publish events to Kafka via `dotnet-kafka-bridge`. Java consumers (Camel/Jackson) deserialize those events using Jackson's default `CamelCaseStrategy`. When .NET serialized with `System.Text.Json` defaults (PascalCase), Java consumers received null fields for every property, throwing exceptions and routing all messages to the DLQ.

**Symptom:** Batch demo never progressed — `processedRecords` stayed at 0, batches stuck in `Processing`.

### Root Cause

`KafkaBridgeRuntime.PublishAsync` used `JsonSerializer.Serialize(payload)` with no options, which defaults to PascalCase:

```json
{"IssuanceId":"...","AccountId":"...","PolicyNumbers":[...],"CompletedAt":"..."}
```

Java's Jackson expected:

```json
{"issuanceId":"...","accountId":"...","policyNumbers":[...],"completedAt":"..."}
```

### Decision

**Use Option A: centralized `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`** in `KafkaBridgeRuntime`.

All Kafka publications from all handlers (`PolicyIssuedEventHandler`, `FileBatchEventHandler`, `IssuanceFailedEvent`) flow through the single `KafkaBridgeRuntime.PublishAsync` method. Fixing it there is a one-line change that covers all current and future event types without touching individual event classes.

### Change

**File:** `dotnet/dotnet-kafka-bridge/Infrastructure/KafkaBridgeRuntime.cs`

Added a static `JsonSerializerOptions` instance:

```csharp
private static readonly JsonSerializerOptions KafkaJsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

And passed it to `JsonSerializer.Serialize(payload, KafkaJsonOptions)`.

### Convention Going Forward

- **All .NET → Kafka serialization must use `JsonNamingPolicy.CamelCase`** to match Java Jackson defaults.
- Do NOT add `[JsonPropertyName]` attributes to shared event classes in `Middleware.Contracts` — the central serializer option is the canonical fix and avoids clutter on the contract types.
- If a new Kafka publish path is ever added outside `KafkaBridgeRuntime`, it must also use `KafkaJsonOptions` or reference the same options instance.

### Verification

After rebuild and container restart, a 3-record batch completed with `status: Completed, processedRecords: 3`. Kafka topic `policy.events.policy-issued` messages confirmed camelCase format:

```json
{"issuanceId":"728ffada-fe9a-409d-b9f4-08db64f6a6fc","accountId":"ACC-ACME-001","policyNumbers":["DC-COMM-728FFADA"],"completedAt":"2026-05-27T15:32:51.0999902+00:00"}
```

---

## Decision: dotnet-platform-integration startup ordering fix

**Decision ID:** dotnet-platform-integration-startup-fix  
**Status:** Resolved (operational fix applied; structural gap documented)  
**Date:** 2026-05-27  
**Author:** DotNet  

---

### Problem

`dotnet-platform-integration` container was in `Created` state and never started after `docker compose up`. The service has `depends_on: condition: service_healthy` on `dotnet-policy-issuance`, `dotnet-billing-finance`, and `dotnet-customer-identity` — all of which have a 60-second `start_period`. If the initial compose up ran before these dependencies became healthy, Docker Compose may have left the integration container in `Created` without retrying the startup.

As a result, the `dotnet-platform-integration` SQL queue table was never created. When `dotnet-policy-issuance` published `IssuePolicyRequestedEvent`, NServiceBus could not deliver it (SQL Error 208 — table not found) and moved the `AccountServiceRecordRetrievedEvent` handler messages to the `error` queue, leaving all in-flight issuances stuck at the `AwaitingPAS` state.

### Root Cause

- NServiceBus SQL transport creates queue tables on endpoint startup. If the endpoint never starts, the table never exists.
- The `SubscriptionRouting` table correctly routes `IssuePolicyRequestedEvent` to `dotnet-platform-integration@[dbo]@[middleware_nsb]`, so the routing itself was correct.
- The `depends_on` ordering is logically correct but brittle: if Docker Compose decides the dependent containers are not healthy fast enough during a cold start, it may not re-attempt the dependent service.

### Fix Applied

1. `docker compose up -d dotnet-platform-integration` — container started, queue tables auto-created by NServiceBus.
2. 2 stranded `AccountServiceRecordRetrievedEvent` messages moved from `dbo.error` back to `dbo.dotnet-policy-issuance` via direct SQL INSERT, allowing the saga to re-process them.
3. Error queue confirmed empty after retry.

### Decision

**`dotnet-platform-integration` must always be started as part of the full stack.** It is an infrastructure-critical service: without its queue table, any `IssuePolicyRequestedEvent` publish will cause a `QueueNotFoundException` and halt all in-flight issuances.

### Structural Recommendation (non-blocking for current sprint)

Consider one of:
- Adding `dotnet-platform-integration` to a reliable startup script that retries until all `depends_on` services are healthy, **then** explicitly starts it.
- OR adding a readiness probe / init-container pattern that pings `dotnet-policy-issuance/health`, `dotnet-billing-finance/health`, and `dotnet-customer-identity/health` before integration starts, rather than relying on Docker Compose dependency resolution timing.
- OR separating the `depends_on` for queue initialization (sqlserver + sqlserver-init) from the service-ordering constraints, so NServiceBus can create its queue tables immediately and the service starts listening once stubs are healthy — this would remove the fragile "wait for sibling services" constraint entirely.

### Verification

Live issuance `f43d16ea-4403-470a-9214-aa5f6f3157b2` reached `Completed` with policy `DC-COMM-F43D16EA`. Full EDA_FLOW trace confirmed in Loki. Error queue: 0 rows.

---

## Decision: Flow Diagram Dynamic Fixes (2026-05-27)

**Decision ID:** frontend-flow-dynamic-fixes  
**Status:** Implemented  
**Date:** 2026-05-27  
**Author:** Frontend  

---

### Context

The ops page sequence diagram at `/ops/[issuanceId]` was always showing the static `UC1_STEPS` fallback topology instead of live `EDA_FLOW` events from Loki. Steven reported the diagram "looks the same" and "should be dynamic but didn't change."

### Root Causes Found

#### 1. Platform-UI Container Was Not Rebuilt
The `/api/policies/[issuanceId]/flow/route.ts` Loki proxy route existed in source code but the container had never been rebuilt to include it. Every call to the flow API returned a 404 HTML page, making `flowData?.events` undefined and `liveSteps.length === 0` permanently. The ops page correctly falls back to static when events are empty — the fallback logic was fine; the container was just stale.

#### 2. Deduplication Key Included `direction`
Every Kafka hop is logged twice in Loki:
- Once as `published` (from the sending service's `EDAFlowProcessor`)
- Once as `consumed` (from the receiving service's `EDAFlowProcessor`)

The dedup key was `messageType|from|to|direction`. Because `published ≠ consumed`, both copies survived dedup, giving the diagram doubled arrows for every hop.

**Fix:** Changed key to `messageType|from|to` only. 16 raw Loki entries → 11 unique edges for a complete UC1 flow.

#### 3. `TOPIC_TO_CONSUMER["policy.events.policy-issued"]` Mapped to `"PolicyIssuance"`
The Java `EDAFlowProcessor` in `policy-issuance-service` had `policy.events.policy-issued → PolicyIssuance` as the consumer. This caused the final step to render as a self-loop (`PolicyIssuance → PolicyIssuance`) instead of the correct terminal arrow to `Notification`.

**Fix:** Changed mapping to `"Notification"`. Also added `compliance.commands.request-compliance-check` mappings (publisher=PolicyIssuance, consumer=Compliance, type=RequestComplianceCheckCommand) to fix UC3 batch flow entries that previously showed `EDA_To = "?"`.

#### 4. Health Check Used `localhost` Which Resolves to IPv6 in Alpine
The healthcheck was `wget http://localhost:3000/`. Alpine Linux resolves `localhost` to `::1` (IPv6), but Next.js standalone only binds to IPv4 by default. Additionally, Docker sets `HOSTNAME` to the container ID, so Next.js bound to the container ID hostname rather than `0.0.0.0`.

**Fix:**
- Added `HOSTNAME: "0.0.0.0"` to platform-ui environment in `docker-compose.yml`
- Changed healthcheck from `http://localhost:3000/` to `http://127.0.0.1:3000/`

### Files Changed

| File | Change |
|------|--------|
| `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` | Removed `direction` from dedup key |
| `java/policy-issuance-service/src/main/java/.../observability/EDAFlowProcessor.java` | Fixed `policy.events.policy-issued` consumer to `Notification`; added `compliance.commands.request-compliance-check` to all three topic maps |
| `docker-compose.yml` | Added `HOSTNAME: "0.0.0.0"` to platform-ui environment; changed healthcheck to use `127.0.0.1` |

### Decisions

1. **Dedup key for Loki flow events must NOT include `direction`** — published and consumed are two observations of the same logical edge. The canonical representation for the sequence diagram is one arrow per `from|to|messageType` triple, irrespective of which service observed it.

2. **`TOPIC_TO_CONSUMER` and `TOPIC_TO_PUBLISHER` in `EDAFlowProcessor` are the source of truth for participant→topic routing** — they must be kept in sync with actual `IssuanceSagaRoute` Kafka endpoint wiring. When new topics are added or changed, both maps must be updated.

3. **Next.js standalone in Docker requires `HOSTNAME: "0.0.0.0"` and healthchecks must use `127.0.0.1`** — `localhost` resolves to IPv6 in Alpine; Docker sets `HOSTNAME` to the container ID.

### Verification

- Flow API at `http://localhost:3000/api/policies/{id}/flow` returns 11 correctly-shaped events for a completed UC1 issuance
- Events: `API→PolicyIssuance, PolicyIssuance→Compliance, Compliance→PolicyIssuance, PolicyIssuance→CustomerIdentity, CustomerIdentity→PolicyIssuance, PolicyIssuance→Integration, Integration→PolicyIssuance, Integration→Billing (fan-out), CustomerIdentity→PolicyIssuance, Billing→PolicyIssuance, PolicyIssuance→Notification`
- Ops page shows "📡 Live — from Loki" badge; `isLiveMode = true`
- Platform-UI container: **healthy** ✓
