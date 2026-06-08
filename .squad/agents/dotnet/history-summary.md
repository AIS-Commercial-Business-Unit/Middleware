# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — event-driven insurance middleware spanning Java and .NET services
- **Stack:** C# 12, .NET 8 LTS, NServiceBus 8.x, SQL Server transport, MongoDB persistence, ASP.NET Core, Serilog, OpenTelemetry
- **Created:** 2026-05-27T05:50:30-04:00

## Key Patterns Established

### Event-Driven Architecture (Udi Dahan Pattern)
- Cross-service orchestration uses pub/sub events, not direct commands
- All saga triggers are published as domain events; sagas subscribe to them
- Fan-out via event subscriptions (multiple handlers subscribe to same event)
- Enforced on UC1 (policy issuance) and UC4 (document retrieval) flows

### EDA Observability — EDA_FLOW Pipeline Logging
- EDAFlowBehavior emits Serilog properties for Loki sequence diagrams
- Correlation fallback: AppraisalId → CorrelationId → RequestId → RequestId
- Hop-by-hop logging: incoming (consumed), outgoing (published), handler (handled)
- ParticipantMap resolves endpoint/handler names to stable UI labels

### UC1 Policy Issuance (Complete & Verified)
- UC1 complete flow: API→PolicyIssuance→Compliance→PAS Response (fan-out)→Billing+CustomerIdentity→Notification
- All 6 UC1 tests pass; cross-stack observability unified with Java
- SQL transport with direct-send fallback for reliability

### UC4 Appraisal Documents (In Progress)
- Two flows: DocumentListSaga (scatter-gather from AtWork + Mainframe) and DocumentRetrievalSaga (content-based routing)
- Mainframe aggregation moved off saga rows to SQL side tables (mf_list_headers, mf_list_parts, mf_document_headers, mf_document_chunks)
- Replaced homegrown callback registry with NServiceBus.Callbacks 4.0.3
- Gateway pattern: 5 external system gateways (IBM MQ, PLUW, PLAPR, Masterpiece, CustomerDB) abstracted behind interfaces
- Both flows now use pub/sub fan-out instead of direct commands or inline calls

### Critical Conventions
- **Kafka Serialization:** All .NET → Kafka uses JsonNamingPolicy.CamelCase (Java Jackson default)
- **JVM Memory Rule:** Xmx ≤ 50% of container mem_limit for Java services
- **BusyBox Health Checks:** Use 127.0.0.1 (explicit IPv4), not localhost (resolves to IPv6)
- **NServiceBus Cancellation:** All gateway calls must pass context.CancellationToken or CancellationToken.None (NSB0002 analyzer enforced)
- **Handler Testing:** Use Data ??= new SagaData() for NServiceBus.Testing compatibility

### Learnings Archive
- **2026-05-27:** UC1 EDA architecture (events vs commands, EDAFlowBehavior, fan-out patterns)
- **2026-05-28:** UC4 gateways, orchestrator vs choreography, static runtime wiring pattern
- **2026-05-31:** Scatter-gather implementation, NServiceBus.Callbacks migration, accumulator repository pattern
- **2026-06-01:** Mainframe MQ accumulation moved to SQL side tables
- **2026-06-07:** Saga event-start pattern reinforcement (removed StartMainframeDocumentAggregationCommand)

## Current Test Status
- 20/20 dotnet tests passing
- All UC1 flows verified end-to-end
- UC4 document retrieval and aggregation tested

## Next Steps
- Document pattern in team runbook for future saga development
- Apply saga-from-events pattern to remaining UC4 services as needed
- Complete UC4 QA validation with live event tracing
