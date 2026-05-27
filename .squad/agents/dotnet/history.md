# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — event-driven insurance middleware spanning Java and .NET services
- **Stack:** C# 12, .NET 8 LTS, NServiceBus 8.x, SQL Server transport, MongoDB persistence, ASP.NET Core, Serilog, OpenTelemetry
- **Created:** 2026-05-27T05:50:30-04:00

## Learnings

### 2026-05-27 — UC1 EDA publish/subscribe correction

- **Cross-service orchestration in the .NET issuance flow must use events, not direct commands.** `IssuanceSaga` now publishes `PolicyIssuanceInitiatedEvent`, `AccountLookupRequestedEvent`, and `IssuePolicyRequestedEvent`, while downstream services subscribe and react autonomously. This is the required pattern for inter-service work in the .NET stack.

- **`PolicyAdminSystemResponseReceivedEvent` is the fan-out point after PAS confirmation.** Billing and customer updates are no longer commanded by the saga; `dotnet-billing-finance` and `dotnet-customer-identity` both subscribe to the PAS response event and emit their own completion events back to the saga.
