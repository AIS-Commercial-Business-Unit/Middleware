# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — event-driven insurance middleware spanning Java and .NET services
- **Stack:** C# 12, .NET 8 LTS, NServiceBus 8.x, SQL Server transport, MongoDB persistence, ASP.NET Core, Serilog, OpenTelemetry
- **Created:** 2026-05-27T05:50:30-04:00

## Summary of Learnings (Through 2026-05-31)

**UC1 EDA Architecture (Complete & Verified):**
- Cross-service orchestration uses pub/sub events, not direct commands (Udi Dahan pattern)
- Live EDA_FLOW observability: `EDAFlowBehavior` emits Serilog properties for sequence diagrams
- `ParticipantMap` resolves endpoint names to UI labels (e.g., dotnet-policy-issuance → PolicyIssuance)
- UC1 complete flow: API→PolicyIssuance→Compliance→PAS Response (fan-out)→Billing+CustomerIdentity→Notification
- All 6 UC1 tests pass; cross-stack observability unified with Java

**Platform-Integration Container Startup Pattern:**
- NServiceBus SQL transport auto-creates queue tables on endpoint startup
- Container startup order matters; if integration service never starts, queues never created
- Fixed: docker compose up -d dotnet-platform-integration
- Decision: must explicitly start or improve depends_on retry logic

**Kafka Serialization Convention:**
- All .NET → Kafka serialization uses `JsonNamingPolicy.CamelCase` (Java Jackson default)
- Centralized in `KafkaBridgeRuntime.PublishAsync` via static `KafkaJsonOptions`
- Root cause fix: PascalCase (C# default) → camelCase (Java default) mismatch halted batch processing
- Verified: batch demo completed with correct JSON format

**UC4 Architecture & Gateway Pattern (Established):**
- 5 external system gateways abstracted behind interfaces (IBM MQ, PLUW, PLAPR, Masterpiece, CustomerDB)
- Saga orchestration over NServiceBus; gateway stubs in adapter layer
- EDAFlowBehavior correlation fallback: AppraisalId → CorrelationId → RequestId
- EC4 spans both Java (prs-appraisal-service) and .NET (dotnet-prs-appraisal); cross-stack parity

## Detailed Learnings

### 2026-05-27 — UC1 EDA Architecture (Condensed Summary)
- EDA_FLOW pipeline logging: `EDAFlowBehavior` emits Serilog properties for Loki sequence diagrams
- ParticipantMap: endpoint names translated to UI labels for consistent rendering
- Cross-service via pub/sub events (not direct commands); fan-out point: `PolicyAdminSystemResponseReceivedEvent`
- Live issuance 232eb4f4: full flow API→PolicyIssuance→Compliance→Integration→(fan-out)→Billing+CustomerIdentity→Notification
- Platform-integration startup: NServiceBus auto-creates queue tables; if service never starts, queues don't exist
- SQL transport fallback: direct-send plus publish ensures reliability with canonical event semantics
- Duplicate collapsing: dedup key `messageType|from|to` ensures rendered sequence matches canonical UC1 shape

### 2026-05-27 — Kafka camelCase Serialization Convention
- Root cause: `KafkaBridgeRuntime.PublishAsync` produced PascalCase JSON (C# default); Java Jackson expects camelCase
- Fix: static `JsonSerializerOptions KafkaJsonOptions` with `PropertyNamingPolicy = CamelCase` at bridge
- Convention: all .NET→Kafka serialization uses `CamelCase`; no per-class `[JsonPropertyName]` attributes
- Verified: batch demo completed with correct camelCase format in Kafka topics

- **dotnet-3 startup ordering pattern established:** Container startup sequencing revealed infrastructure-critical pattern — NServiceBus endpoints must start to create their queue tables. Decision documented; structural hardening recommendations provided for future sprint.

- **frontend-3 corrections unified all 4 agents' observability contract:** Flow diagram fixes (stale container + dedup key + TOPIC_TO_CONSUMER mapping + health check) enabled live Loki-backed visualization of entire cross-stack architecture. Platform-UI now shows real topology matching Java/Camel and .NET/NServiceBus pub/subscribe.

- **Decisions archive:** 4 inbox files merged into unified `.squad/decisions/decisions.md`. Serialization, startup, and flow conventions now central reference for team-wide cross-stack interop.

### 2026-05-28 — UC4 Appraisal Documents (.NET stack built)

- **`dotnet-prs-appraisal` created as a new NServiceBus service (port 8189).** Full saga: `AppraisalReceivedSaga` with parallel join (PLUWAppraisalCreatedEvent + UWAssignmentDeterminedEvent), NServiceBus timeout handling (`RequestTimeout<AppraisalSagaTimeoutMessage>`), and content-based routing for StatusCodes 6, 15, and generic. Key file: `dotnet/dotnet-prs-appraisal/Sagas/AppraisalReceivedSaga.cs`.

- **`dotnet-customer-identity` extended with `ProducerLookupHandler`.** New handler subscribes to `ProducerLookupRequestedEvent` and publishes `ProducerCrossReferenceRetrievedEvent`. Uses in-memory seed data (5 demo scenarios with `REPLACE_ME_` constants). Key file: `dotnet/dotnet-customer-identity/Handlers/ProducerLookupHandler.cs`.

- **10 new UC4 message contracts added to `Middleware.Contracts`.** Events: `RiskIDStatusUpdateReceivedEvent`, `ProducerLookupRequestedEvent`, `ProducerCrossReferenceRetrievedEvent`, `PLUWAppraisalCreateRequestedEvent`, `PLUWAppraisalCreatedEvent`, `UWDeterminationRequestedEvent`, `UWAssignmentDeterminedEvent`, `AppraisalUnderwriterAssignedEvent`, `AppraisalCompletedEvent`, `AppraisalStatusUpdateFailedEvent`. Command: `ProcessAppraisalStatusUpdateCommand`.

- **Static `AppraisalRuntime` pattern used for gateway wiring.** Consistent with existing `CustomerIdentityRuntime` pattern. NServiceBus handlers access gateways via `AppraisalRuntime.PLUWGateway`, `AppraisalRuntime.MasterpieceGateway`, etc. Gateways wired at `Program.cs` startup before `NServiceBus.Endpoint.Start()`.

- **NSB0002 (cancellation token) enforced by NServiceBus analyzer.** All gateway calls from handlers must explicitly pass `context.CancellationToken` or `CancellationToken.None`. Failing to do so fails the build.

- **Gateway stubs follow observable `⚠️ STUBBED:` log pattern.** All 5 gateways (RiskIDMQ, PLUW, PLAPR, Masterpiece, CustomerDB) log `LogWarning` with `⚠️ STUBBED:` prefix and `REPLACE_ME_*` constant. PLAPR stub uses MongoDB collection `plapr_staging` for demo visibility.

- **`EDAFlowBehavior` uses `EDA_IssuanceId` property name (not `EDA_AppraisalId`) for Loki compatibility.** The Loki query and Platform UI were built against `EDA_IssuanceId` — using the same property name means the UC4 appraisal flows appear in existing dashboards. The correlation key stores the `appraisalId` value.

- **Service port assignments:** dotnet-prs-appraisal = 8189. All existing ports unchanged.

### 2026-05-31T16:58:30.069-04:00 — UC4 EDA_FLOW hop logging for dotnet-prs-appraisal

- **UC4 document commands now correlate `EDA_FLOW` logs with `CorrelationId` / `RequestId` fallbacks.** `dotnet-prs-appraisal/Behaviors/EDAFlowBehavior.cs` now extracts `AppraisalId`, then `CorrelationId`, then `RequestId`, so UC4 document list/retrieval commands, events, and timeout messages all emit Loki-compatible `EDA_IssuanceId` values.

- **HTTP controller, sagas, and Artemis listeners now emit explicit hop-by-hop `EDA_FLOW` logs for the UC4 document flow.** `AppraisalDocumentsController`, `DocumentListSaga`, `DocumentRetrievalSaga`, `ArtemisListReplyListener`, and `ArtemisDocumentReplyListener` now log API, AtWork, Mainframe, and saga hops with consistent `EDA_*` properties and channel names so platform-ui can render live sequence diagrams from Loki.

- **`AppraisalParticipantMap` now recognizes UC4 document participants and saga destinations.** Added `AtWork`, `Mainframe`, `deipde07-mq-simulator`, and the UC4 document message→subscriber mappings so outgoing/incoming pipeline logs resolve to stable participant labels instead of falling back to opaque endpoint names or skipping the flow.

### 2026-05-31T16:58:30.069Z — UC4 EDA_FLOW completion + Scribe decision merge

- **EDAFlowBehavior correlation fallback (AppraisalId → CorrelationId → RequestId) is now the canonical pattern for multi-domain flows.** This enables any future UC or cross-stack flow that uses different correlation keys to emit the same Loki-compatible EDA_IssuanceId field without behavior code changes.

- **Decision #44 (Comprehensive UC4 EDA_FLOW hop logging) merged into squad/decisions.md.** This completes the team's acknowledgment that hop-by-hop observability is required before ops sequence diagrams can claim to reflect actual topology.

- **Observable cross-checks:** dotnet build: 0 errors, 0 warnings. All existing UC1 tests pass. UC4 tests still pending (Decision #46: add .NET test project + JSON logging).

### 2026-05-31T19:35:23-04:00 — UC4 Scatter-Gather Implementation & NServiceBus.Callbacks Migration

- **Implemented proper EDA scatter-gather pattern via `Uc4AppraisalDocumentListRequestedEvent`.** `DocumentListSaga` now publishes event instead of calling AtWork inline. Created `AtWorkDocumentListHandler` subscribing to event and publishing `Uc4AtWorkDocumentListCompletedEvent`. Mainframe handler also subscribes to same event. Saga coordinates both sources, eliminating temporal coupling. **Commit:** 7d86cb3

- **Replaced homegrown `ICallbackRegistry`/`CallbackRegistry` with NServiceBus.Callbacks 4.0.3.** Updated `PolicyIssuanceController` to use `messageSession.Request<T>()` for callback-style messaging. All sagas now use `context.Reply()` instead of custom registry calls. Removed dead files: `CallbackRegistry.cs`, `ICallbackRegistry.cs`, `DocumentListResult.cs`. **Commit:** 2e7d9b5

- **UC4 EDA compliance review findings documented by Architect.** 3 critical violations (scatter-gather not published, temporal coupling, inline AtWork calls) — all fixed by this session's scatter-gather implementation. 3 important issues (command vs event pattern I1, infrastructure in domain layer I2, missing completion event I3) — fixed in commits above. Decision merged to `decisions.md` for team reference. Next: Review whether DocumentRetrievalSaga needs same scatter-gather pattern (C3 priority).
