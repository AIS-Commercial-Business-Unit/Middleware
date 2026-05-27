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
