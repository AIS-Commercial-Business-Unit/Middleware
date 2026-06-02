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
- Decision: must explicitly start or improve depends_on retry logic

**Kafka Serialization Convention:**
- All .NET → Kafka serialization uses `JsonNamingPolicy.CamelCase` (Java Jackson default)
- Centralized in `KafkaBridgeRuntime.PublishAsync` via static `KafkaJsonOptions`
- Root cause fix: PascalCase (C# default) → camelCase (Java default) mismatch halted batch processing

**UC4 Architecture & Gateway Pattern (Established):**
- 5 external system gateways abstracted behind interfaces (IBM MQ, PLUW, PLAPR, Masterpiece, CustomerDB)
- Saga orchestration over NServiceBus; gateway stubs in adapter layer
- EDAFlowBehavior correlation fallback: AppraisalId → CorrelationId → RequestId — canonical pattern for multi-domain flows
- UC4 spans both Java (prs-appraisal-service) and .NET (dotnet-prs-appraisal); cross-stack parity
- Hop-by-hop EDA_FLOW logging in HTTP controller, sagas, Artemis listeners — all participants resolved through `AppraisalParticipantMap`
- `dotnet-prs-appraisal` runs on port 8189; `EDA_IssuanceId` property name reused (not `EDA_AppraisalId`) for Loki dashboard compatibility
- `dotnet-customer-identity` extended with `ProducerLookupHandler` (5 demo scenarios; `REPLACE_ME_` constants)
- Static `AppraisalRuntime` / `CustomerIdentityRuntime` for gateway wiring; NSB0002 enforces explicit `CancellationToken` from handlers
- Gateway stubs use `⚠️ STUBBED:` log prefix for demo observability

> Older detailed entries archived in `history-archive.md`.

### 2026-05-31T19:35:23-04:00 — UC4 Scatter-Gather Implementation & NServiceBus.Callbacks Migration

- **Implemented proper EDA scatter-gather pattern via `Uc4AppraisalDocumentListRequestedEvent`.** `DocumentListSaga` now publishes event instead of calling AtWork inline. Created `AtWorkDocumentListHandler` subscribing to event and publishing `Uc4AtWorkDocumentListCompletedEvent`. Mainframe handler also subscribes to same event. Saga coordinates both sources, eliminating temporal coupling. **Commit:** 7d86cb3
- **Replaced homegrown `ICallbackRegistry`/`CallbackRegistry` with NServiceBus.Callbacks 4.0.3.** Updated `PolicyIssuanceController` to use `messageSession.Request<T>()` for callback-style messaging. All sagas now use `context.Reply()` instead of custom registry calls. Removed dead files: `CallbackRegistry.cs`, `ICallbackRegistry.cs`, `DocumentListResult.cs`. **Commit:** 2e7d9b5
- **UC4 EDA compliance review findings documented by Architect.** 3 critical violations (scatter-gather not published, temporal coupling, inline AtWork calls) — all fixed by this session's scatter-gather implementation. 3 important issues (command vs event pattern I1, infrastructure in domain layer I2, missing completion event I3) — fixed in commits above.

### 2026-05-31T20:03:13-04:00 — UC4 EDA Compliance: C3 + I1 Final Fixes

- **C3 (DocumentRetrievalSaga AtWork inline call) eliminated.** `DocumentRetrievalSaga` now publishes `Uc4AppraisalDocumentRetrievalRequestedEvent` instead of calling `AtWorkFixture.BuildRetrievalResult()` inline. Created `AtWorkDocumentRetrievalHandler` subscribing to that event, calling the fixture, and publishing `Uc4AtWorkDocumentRetrievedEvent`. Saga handles the async reply via `Handle(Uc4AtWorkDocumentRetrievedEvent)` with a `TryCompleteAtWorkAsync()` guard (same `TryComplete` pattern as `DocumentListSaga`). Saga state additions: `AtWorkPending`, `AtWorkDone`, `AtWorkContent`, `AtWorkMimeType`. New contracts: `Uc4AppraisalDocumentRetrievalRequestedEvent`, `Uc4AtWorkDocumentRetrievedEvent`. **Commit:** 96914d6
- **I1 (MainframeListAggregatorSaga started by command) fixed.** `MainframeListAggregatorSaga` is now started by `Uc4AppraisalDocumentListRequestedEvent` directly, eliminating `MainframeDocumentListAdapterHandler` and `StartMainframeListAggregationCommand` which were pure ceremony.
- **Pattern: Sagas started by events, not commands, wherever the trigger is a pub/sub notification.** If a saga's sole purpose is to receive an event and forward a command to itself, collapse them — the saga subscribes to the event directly.
- **Pattern: All saga `Handle(startMessage)` methods use `Data ??= new SagaData()` for test-harness compatibility.**
- **20/20 tests pass post-fix.** Build: 0 errors, 0 warnings.

### 2026-05-31T21:09:38.779-04:00 — UC4 handler-invocation EDA_FLOW fan-out logging

- **`EDAFlowBehavior` now logs one `handled` entry per subscriber invocation using `Behavior<IInvokeHandlerContext>`.** `AppraisalEDAFlowHandlerInvokeBehavior` resolves `context.MessageHandler.HandlerType.Name` through `AppraisalParticipantMap.ResolveHandler(...)`, so fan-out events render separate arrows for each subscriber instead of a single generic receive.
- **Outgoing logs emit `EDA_Handler = "n/a"`** so the frontend can distinguish publish arrows from subscriber-handled arrows while keeping the existing incoming `consumed` logs for fallback/dedup.
- **Verification:** `dotnet build` succeeded; `dotnet test` passed 20/20.

### 2026-06-01 — Per-host ingress + APIM parity for the two .NET HTTP services
- Platform delivered ingress + private DNS A records for `dotnet-policy-issuance` (`dotnet-policy.middleware.internal`) and `dotnet-file-processing` (`dotnet-file-processing.middleware.internal`); both resolve to the shared ingress ILB (10.0.16.10). The other 7 .NET services are NServiceBus event-only and remain cluster-internal.
- Azure provisioned APIM APIs `dotnet-policy-issuance-api` / `dotnet-file-processing-api` plus per-API backends pointing at the new hostnames. Each API's `policy.xml` does `<set-backend-service backend-id="dotnet-{name}" />` per decision #50.
- `dotnet-` prefix is uniform end-to-end (folder, displayName suffix, APIM path, backend id, host). Java stack keeps bare names — no client churn.
- Both stacks now addressable concurrently through APIM via path-prefix split (`/policy-issuance` vs `/dotnet-policy-issuance`). Same APIM instance, same product, same subscription model.
- Decisions: #51 (Platform per-host ingress) + #52 (Azure APIM dual-stack).
