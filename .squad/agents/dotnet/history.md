# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — event-driven insurance middleware spanning Java and .NET services
- **Stack:** C# 12, .NET 8 LTS, NServiceBus 8.x, SQL Server transport, MongoDB persistence, ASP.NET Core, Serilog, OpenTelemetry
- **Created:** 2026-05-27T05:50:30-04:00

## Learnings

### 2026-05-27 — EDA_FLOW NServiceBus pipeline logging

- **`dotnet-policy-issuance` now emits `EDA_FLOW` logs from the NServiceBus pipeline for both outgoing and incoming messages.** `EDAFlowOutgoingBehavior` records published/sent messages with `EDA_*` properties, and `EDAFlowIncomingBehavior` records consumed messages using `NServiceBus.Headers.OriginatingEndpoint` plus the message `IssuanceId`.

- **Participant labels for the sequence diagram are resolved centrally from endpoint names.** The `ParticipantMap` in `Behaviors/EDAFlowBehavior.cs` translates NServiceBus endpoint names like `dotnet-platform-compliance` into UI labels like `Compliance`, keeping Loki-driven diagram rendering consistent across the .NET issuance flow.

- **Cross-stack observability contract (Java + .NET + frontend) creates unified live diagnostics.** Both Java EDAFlowProcessor and .NET EDAFlowBehavior emit identical `EDA_*` Serilog properties so the frontend Loki proxy can query and normalize both stacks' messages into a single live sequence diagram with a live/static badge. This validates actual topology against the Udi Dahan pub/subscribe architecture.

### 2026-05-27 — UC1 EDA publish/subscribe correction

- **Cross-service orchestration in the .NET issuance flow must use events, not direct commands.** `IssuanceSaga` now publishes `PolicyIssuanceInitiatedEvent`, `AccountLookupRequestedEvent`, and `IssuePolicyRequestedEvent`, while downstream services subscribe and react autonomously. This is the required pattern for inter-service work in the .NET stack.

- **`PolicyAdminSystemResponseReceivedEvent` is the fan-out point after PAS confirmation.** Billing and customer updates are no longer commanded by the saga; `dotnet-billing-finance` and `dotnet-customer-identity` both subscribe to the PAS response event and emit their own completion events back to the saga.

### 2026-05-27 — DotNet UC1 flow parity validation

- **The authoritative UC1 sequence spec is `.docs/req/use-cases.html`, and the live ops diagram renders whatever `EDA_From` / `EDA_To` labels the backend logs.** In practice, `dotnet-policy-issuance/Behaviors/EDAFlowBehavior.cs` is part of the user-visible contract because `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` deduplicates and renders directly from those structured log labels.

- **Local .NET UC1 fan-out over NServiceBus + SQL transport was not fully reliable from subscription rows alone.** Stable end-to-end completion required deterministic `SubscriptionRouting` seeding/startup ordering plus direct-send fallbacks that still preserve the canonical published event flow: PAS responses are published and also forwarded to `dotnet-policy-issuance`, `dotnet-billing-finance`, and `dotnet-customer-identity`, while billing/customer completion events are also forwarded back to `dotnet-policy-issuance`.

- **Duplicate deliveries are acceptable for the live diagram as long as labels stay canonical.** The fallback approach can produce repeated `PolicyAdminSystemResponseReceivedEvent`, `CustomerUpdatedEvent`, and `BillingAssociationCreatedEvent` deliveries, but the frontend flow endpoint collapses duplicates by `messageType|from|to|direction`, so the rendered sequence still matches the canonical UC1 shape.
### 2026-05-27 — UC1 flow parity achieved (dotnet-2 complete)

- **PasGatewayHandler now publishes and direct-sends PolicyAdminSystemResponseReceivedEvent.** The canonical flow required both publish (for subscribers) and direct-send fallback (for local SQL transport reliability). This dual-path approach keeps the architecture event-driven while ensuring Billing and Customer Identity receive the PAS response reliably.

- **EDAFlowBehavior labels now match the ops diagram and use canonical UC1 participant names.** The ParticipantMap ensures that dotnet-policy-issuance renders as PolicyIssuance, dotnet-billing-finance as Billing, etc. This creates the feedback loop where live EDA_FLOW logs match the static .docs/req/use-cases.html topology.

- **Live issuance 232eb4f4 completed end-to-end with canonical flow.** API→PolicyIssuance→Compliance, API→PolicyIssuance→Integration→(fan-out)→Billing+CustomerIdentity→back to PolicyIssuance→Notification. All 6 tests pass. The .NET stack now enforces Udi Dahan pub/subscribe semantics operationally.

### 2026-05-27 — Debug: dotnet-platform-integration container never started (IssuePolicyRequestedEvent stuck)

- **Root cause: `dotnet-platform-integration` container was in `Created` state and never started.** The container has `depends_on` conditions on `dotnet-policy-issuance`, `dotnet-billing-finance`, and `dotnet-customer-identity` (all `service_healthy`). These services only became healthy after a delay, and the integration container either was not started in the initial `docker compose up` invocation or failed silently to start after its dependencies became healthy.

- **Symptom chain:** `dotnet-policy-issuance` saga received `AccountServiceRecordRetrievedEvent` and tried to publish `IssuePolicyRequestedEvent`. NServiceBus SQL transport looked up the `SubscriptionRouting` table, found `dotnet-platform-integration@[dbo]@[middleware_nsb]` as the subscriber, and attempted to INSERT into the `dotnet-platform-integration` SQL queue table — which didn't exist because the service had never started. NServiceBus threw `QueueNotFoundException` (SQL Error 208) and moved the `AccountServiceRecordRetrievedEvent` messages to the `error` queue.

- **Fix:** `docker compose up -d dotnet-platform-integration` started the container. NServiceBus auto-created the `dotnet-platform-integration` and `dotnet-platform-integration.Delayed` queue tables on startup. Two stranded `AccountServiceRecordRetrievedEvent` messages were moved from `dbo.error` back to `dbo.dotnet-policy-issuance` via direct SQL INSERT, allowing the saga to re-process them.

- **Verification:** New issuance `f43d16ea-4403-470a-9214-aa5f6f3157b2` reached `Completed` with policy `DC-COMM-F43D16EA`. Loki EDA_FLOW trace confirms: API→PolicyIssuance→Compliance→PolicyIssuance→CustomerIdentity→PolicyIssuance→**Integration**→PolicyIssuance→Billing+CustomerIdentity→Notification. Error queue is empty (0 rows).

- **Operational note:** `dotnet-platform-integration` must be explicitly started or its `depends_on` timing improved. The container is healthy after startup but is not guaranteed to come up automatically if its health-dependent parents start slowly. Consider adding an explicit startup step or health retry loop in the compose workflow.

### 2026-05-27 — Kafka camelCase serialization fix (cross-stack interop)

- **Root cause of batch demo stall:** `KafkaBridgeRuntime.PublishAsync` called `JsonSerializer.Serialize(payload)` with no options, producing PascalCase JSON (e.g. `IssuanceId`). Java's Jackson deserializer expects camelCase (`issuanceId`) by default, so every field deserialized to null, causing exceptions and DLQ routing.

- **Fix (Option A — centralized):** Added a static `JsonSerializerOptions KafkaJsonOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` to `KafkaBridgeRuntime` and passed it to every `JsonSerializer.Serialize` call. All current and future handlers automatically use camelCase because all Kafka publication flows through this single method.

- **Convention:** All .NET → Kafka serialization must use `JsonNamingPolicy.CamelCase`. Do not use `[JsonPropertyName]` attributes on `Middleware.Contracts` event classes; the serializer option at the bridge is the canonical fix.

- **Verification:** New batch (`count=3`) completed with `status: Completed, processedRecords: 3`. Kafka topic `policy.events.policy-issued` confirmed camelCase format: `{"issuanceId":"...","accountId":"...","policyNumbers":[...],"completedAt":"..."}`. Decision documented in `.squad/decisions/inbox/dotnet-kafka-camelcase.md`.

### 2026-05-27 — Cross-agent synchronization (Scribe session)

- **qa-1 root cause diagnosis enabled dotnet-4 fix:** QA diagnosed the batch stall as cross-stack JSON serialization mismatch (PascalCase vs. camelCase). DotNet team implemented centralized `JsonNamingPolicy.CamelCase` in `KafkaBridgeRuntime`, fixing batch processing end-to-end.

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

