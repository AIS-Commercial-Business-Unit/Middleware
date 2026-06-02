# DotNet History — Archive

Archived from `history.md` on 2026-06-01 to keep active history under the 15KB threshold. Entries below are preserved verbatim for reference; their key learnings are captured in the "Summary of Learnings" section of `history.md`.

---

### 2026-05-31 — UC4 EDAFlowBehavior Handler Invocation Logging (dotnet-eda-flow-subscribers)

- **Handler-level logging:** `AppraisalEDAFlowHandlerInvokeBehavior` added at `IInvokeHandlerContext` stage to emit `EDA_Direction = "handled"` for each subscriber invocation, not just `consumed` at the dispatcher level.
- **Participant resolution:** Extended `AppraisalParticipantMap` with `HandlerToParticipant(HandlerType)` to map handler classes to readable participant names (e.g., `InvokeAppraisalHandler` → `InvokeAppraisal`).
- **EDA_Handler field:** All handler invocation logs now include `EDA_Handler` with the handler class name, enabling ops UI hover tooltips to identify which subscriber processed each event.
- **Fan-out visibility:** Incoming `consumed` + outgoing `published` logs unchanged; new `handled` entries render as distinct subscriber arrows for fan-out topology. Ops UI preserves `handled` entries (not deduped) for accurate multi-subscriber diagrams.
- **Build status:** 20/20 tests passing. No regression on UC1 or document path flows.
- **Key design:** Behavior registration in `Program.cs` keeps logging layer separate from saga/handler domain code; if observability tool changes, only pipeline configuration affected.

---

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
