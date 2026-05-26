# Getting Started — .NET Stack

This guide walks you through running and understanding the **NServiceBus / .NET** side of the AIS Middleware Platform.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [Rancher Desktop](https://rancherdesktop.io/) or Docker Desktop | Latest | Container runtime |
| .NET 8 SDK | 8.x LTS | For local builds outside Docker |
| PowerShell | 7+ | For running test scripts |

> **Tip:** All services can be built and run entirely inside Docker — you don't need .NET installed locally to run the demos.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Messaging / saga framework | **NServiceBus 8.x** (Particular Software) |
| Message transport | **SQL Server** (NServiceBus.SqlServer transport) |
| Application container | **ASP.NET Core / .NET 8 LTS** |
| Domain persistence | **MongoDB** |
| Structured logging | **Serilog** with JSON sink → Loki |
| Distributed tracing | **OpenTelemetry** → Grafana Tempo |
| Kafka bridge | `dotnet-kafka-bridge` forwards .NET events to shared Kafka topics |

### Why SQL Server for NServiceBus Transport?

NServiceBus supports multiple transports (Azure Service Bus, RabbitMQ, Amazon SQS, SQL Server). This demo uses SQL Server because:
- No additional broker infrastructure needed beyond what's already in the stack
- Familiar to .NET teams coming from MSMQ / Service Broker
- Full saga support with SQL persistence
- Easy to swap to Azure Service Bus for production (`appsettings.json` config change)

---

## Project Layout

```
dotnet/
├── Middleware.sln                    ← Solution file
├── Directory.Build.props             ← Shared MSBuild properties
├── Middleware.Contracts/             ← Shared message contracts (commands + events)
├── dotnet-policy-issuance/          ← UC1: NServiceBus saga + REST entry (port 8181)
├── dotnet-platform-compliance/      ← Sanctions screening handler (port 8182)
├── dotnet-customer-identity/        ← Account lookup + CRM update (port 8183)
├── dotnet-platform-integration/     ← PAS routing handler (port 8184)
├── dotnet-billing-finance/          ← Billing association handler (port 8185)
├── dotnet-platform-notification/    ← Notification handler (port 8186)
├── dotnet-file-processing/          ← UC3: CSV batch processor (port 8187)
├── dotnet-kafka-bridge/             ← Publishes .NET events to Kafka (port 8188)
└── tests/                            ← Unit tests (NServiceBus.Testing)
    ├── PolicyIssuance.Tests/
    ├── Compliance.Tests/
    └── ...
```

---

## Running the .NET Stack

### Option 1: Docker Compose (Recommended)

```bash
# From repository root — starts .NET services + all infrastructure
docker compose up --build

# Start only .NET services
docker compose up --build \
  sqlserver sqlserver-init \
  mongodb \
  kafka zookeeper \
  otel-collector grafana loki prometheus tempo \
  dotnet-policy-issuance dotnet-platform-compliance \
  dotnet-customer-identity dotnet-platform-integration \
  dotnet-billing-finance dotnet-platform-notification \
  dotnet-file-processing dotnet-kafka-bridge \
  platform-ui
```

### Option 2: Local .NET Build

```bash
cd dotnet
dotnet build Middleware.sln

# Run a single service locally (infrastructure must be running in Docker)
cd dotnet-policy-issuance
dotnet run
```

---

## Verifying the Stack Is Running

```bash
# Switch Platform UI to .NET backend
# Set ACTIVE_BACKEND=dotnet in docker-compose.yml → platform-ui section, then rebuild

# Test UC1 — Policy Issuance (.NET backend)
curl -X POST http://localhost:8181/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":"Test User","coverageAmount":100000}'

# Poll saga status
# (replace <issuanceId> with the ID returned above)
curl http://localhost:8181/api/v1/policies/<issuanceId>/status
```

Expected saga progression (watch via `docker logs dotnet-policy-issuance -f`):

```
IssuanceSaga STARTED  issuanceId=<id>
IssuanceSaga → AwaitingCompliance
IssuanceSaga → ComplianceCleared
IssuanceSaga → AccountRecordRetrieved
IssuanceSaga → AwaitingPAS
IssuanceSaga → PASConfirmed
IssuanceSaga COMPLETED  issuanceId=<id>
```

---

## Key Concepts

### NServiceBus Saga

NServiceBus sagas are state machines. Each handler receives a message and transitions the saga to a new state:

```csharp
public class IssuanceSaga : Saga<IssuanceSagaData>,
    IAmStartedByMessages<IssuePolicyCommand>,
    IHandleMessages<ComplianceClearedEvent>,
    IHandleMessages<BillingAssociationCreatedEvent>
{
    public async Task Handle(IssuePolicyCommand message, IMessageHandlerContext context)
    {
        Data.Status = "AwaitingCompliance";
        await context.Send(new RequestComplianceCheckCommand { ... });
    }
}
```

- **`Saga<T>`** — base class; `T` is your saga state (persisted to MongoDB)
- **`IAmStartedByMessages<T>`** — which message creates a new saga instance
- **`IHandleMessages<T>`** — subsequent messages handled by the saga
- **`context.Send()`** — sends a command to one handler
- **`context.Publish()`** — broadcasts an event to all subscribers

### Message Contracts

All commands and events are in `Middleware.Contracts`. This is the shared contract between all services:

```csharp
// Command — sent to one specific handler
public record IssuePolicyCommand : ICommand
{
    public string IssuanceId { get; init; }
    public string PolicyTypeCode { get; init; }
    public string ApplicantName { get; init; }
}

// Event — published to all interested subscribers
public record ComplianceClearedEvent : IEvent
{
    public string IssuanceId { get; init; }
    public string CorrelationId { get; init; }
}
```

### Kafka Bridge

The `dotnet-kafka-bridge` service subscribes to NServiceBus events and re-publishes them to Kafka topics, so the Platform UI Saga Explorer and Grafana dashboards work identically for both stacks.

### Structured Logging with Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.GrafanaLoki(...)
    .CreateLogger();

// In a handler:
using (LogContext.PushProperty("issuanceId", message.IssuanceId))
{
    _logger.LogInformation("IssuanceSaga STARTED");
}
```

All log lines for a given `issuanceId` can be filtered in Grafana Loki:
```
{service="dotnet-policy-issuance"} | json | issuanceId="<your-id>"
```

---

## SQL Server Setup

SQL Server is used exclusively as the **NServiceBus message transport**. The `sqlserver-init` container creates the required database and tables on first run:

```sql
-- NServiceBus creates these tables automatically in the middleware_transport database:
-- dotnet_policy_issuance.IssuePolicyCommand
-- dotnet_platform_compliance.RequestComplianceCheckCommand
-- (one table per endpoint per message type)
```

**Memory requirement:** SQL Server needs at least **2GB RAM** to handle 7+ simultaneous NServiceBus connections. The `docker-compose.yml` sets `mem_limit: 2g` for the SQL Server container.

---

## Observability

| Signal | Where |
|---|---|
| Structured logs (Serilog) | Grafana → Explore → Loki |
| Distributed traces | Grafana → Explore → Tempo |
| Metrics dashboards | Grafana → Dashboards |
| Kafka events (from bridge) | http://localhost:9000 (Kafdrop) |
| MongoDB documents | http://localhost:8888 (Mongo Express) |

---

## Running Unit Tests

NServiceBus provides first-class unit testing via `NServiceBus.Testing`:

```bash
cd dotnet/tests
dotnet test

# Run a specific test project
dotnet test dotnet/tests/PolicyIssuance.Tests/PolicyIssuance.Tests.csproj
```

Example test pattern:

```csharp
[Test]
public async Task Handle_IssuePolicyCommand_SendsComplianceCheck()
{
    var saga = new IssuanceSaga();
    var context = new TestableMessageHandlerContext();

    await saga.Handle(new IssuePolicyCommand
    {
        IssuanceId = "test-123",
        PolicyTypeCode = "1",
        ApplicantName = "Test User"
    }, context);

    Assert.That(context.SentMessages, Has.One.Items);
    Assert.That(context.SentMessages[0].Message,
        Is.TypeOf<RequestComplianceCheckCommand>());
}
```

> NServiceBus has significantly better unit test ergonomics than Apache Camel for saga/handler logic. See [Testing Comparison](.docs/testing-comparison.md).

---

## How Integrations Work: .NET vs Java

| Concern | Java (Apache Camel) | .NET (Logic Apps / NServiceBus) |
|---|---|---|
| External system calls | Camel HTTP component in a route | `IMessageHandler` calling `HttpClient` |
| Routing logic | Content-Based Router in Camel DSL | NServiceBus message routing rules |
| Protocol adapters | Camel components (300+) | Custom handlers or Azure Logic Apps connectors |
| Transformation | Camel processors / JAXB / Jackson | AutoMapper / custom mapping code |

**Key insight:** Apache Camel's component library makes it easy to connect to legacy protocols (FTP, SFTP, JMS, SAP, etc.) without writing boilerplate. NServiceBus is stronger for saga orchestration and testability, but requires more custom code for protocol adapters — or Azure Logic Apps for low-code integration.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| SQL Server OOM (`Error 701`) | Ensure `mem_limit: 2g` is set in `docker-compose.yml` for `sqlserver` |
| NServiceBus transport error on startup | Run `docker compose restart dotnet-policy-issuance` after SQL Server is fully ready |
| `sqlserver-init` keeps failing | Check SQL Server health: `docker logs sqlserver` |
| Saga stuck | Check all 6 .NET handler services are running: `docker compose ps \| grep dotnet` |

---

## Further Reading

- [Running the Demos](.docs/running-the-demos.md) — what UC1 and UC3 demonstrate
- [Testing Comparison](.docs/testing-comparison.md) — NServiceBus vs Apache Camel testability
- [NServiceBus Documentation](https://docs.particular.net/nservicebus/) — official Particular Software docs
