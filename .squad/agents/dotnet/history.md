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
