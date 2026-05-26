# Getting Started — AIS Middleware Platform

> This guide is written alongside the code. Each concept is explained the first time it appears in the codebase. Read it in order, or jump to any section.

---

## Table of Contents

1. [The Project at a Glance](#1-the-project-at-a-glance)
2. [Running the Stack Locally](#2-running-the-stack-locally)
3. [What is a Camel Route?](#3-what-is-a-camel-route)
4. [How Kafka Pub/Sub Works](#4-how-kafka-pubsub-works)
5. [What is a Saga?](#5-what-is-a-saga)
6. [What is a Content-Based Router?](#6-what-is-a-content-based-router)
7. [What is Idempotency and Why Does It Matter?](#7-what-is-idempotency-and-why-does-it-matter)
8. [What is OpenTelemetry?](#8-what-is-opentelemetry)
9. [Structured Logging (Java's Equivalent of Serilog)](#9-structured-logging-javas-equivalent-of-serilog)
10. [Dead-Letter Queues](#10-dead-letter-queues)
11. [The Domain Event Contract](#11-the-domain-event-contract)
12. [Project Layout Reference](#12-project-layout-reference)

---

## 1. The Project at a Glance

This platform replaces BizTalk. Instead of orchestrations and SOAP adapters, it uses:

| Old (BizTalk) | New (This Platform) |
|---|---|
| BizTalk Orchestration | Apache Camel Route |
| MSMQ / Service Bus | Kafka Topic |
| BizTalk Artifact (send/receive ports) | Domain Event or Command |
| SQL Server | MongoDB |
| BizTalk Admin Console | Grafana + Kafdrop |

**The stack:**
- **Apache Camel** — defines how messages move through the system (routes)
- **Kafka** — the event bus; services publish and subscribe to topics
- **MongoDB** — each service has its own database (no shared schema)
- **Spring Boot** — the application container for each service
- **OpenTelemetry** — wires all services together into one distributed trace
- **Grafana** — dashboards, logs (Loki), and traces (Tempo)
- **Next.js / React** — Platform.UI for demos and the Saga Explorer

---

## 2. Running the Stack Locally

**Prerequisites:** Rancher Desktop (or Docker Desktop) must be running.

```bash
# From the repository root:
docker compose up --build

# Once running:
# Platform UI:     http://localhost:3000
# Kafka UI:        http://localhost:9000
# MongoDB UI:      http://localhost:8888
# Grafana:         http://localhost:3001  (admin / admin)
# Prometheus:      http://localhost:9090
```

**To submit a policy issuance:**
1. Open http://localhost:3000
2. Select a policy type and click **Submit IssuePolicy Command**
3. You are redirected to the Saga Explorer — watch the saga advance through states in real time
4. Open Grafana → Explore → Loki to see structured logs with `issuanceId` filter
5. Open Grafana → Explore → Tempo to see the full distributed trace

---

## 3. What is a Camel Route?

**First appears in:** `policy-issuance-service/src/main/java/.../routes/IssuanceSagaRoute.java`

A **Camel Route** is a pipeline that describes how a message moves from a source to a destination, with processing steps in between.

```java
from("kafka:compliance.events.compliance-cleared?groupId=policy-issuance-saga")
    .log("Compliance cleared for issuanceId=${header.issuanceId}")
    .process(exchange -> {
        // Read the event, update saga state, prepare next command
    })
    .to("kafka:customer.commands.get-or-create-account-record");
```

Think of it like this:

```
kafka topic (compliance-cleared)
    ↓  read message
    ↓  process: update MongoDB saga state
    ↓  write message
kafka topic (get-or-create-account-record)
```

**Key terms:**
- `from(...)` — where the message comes from (a Kafka topic, a timer, an HTTP endpoint)
- `to(...)` — where the message goes (another Kafka topic, an HTTP endpoint, a log)
- `process(exchange -> ...)` — arbitrary Java code that reads and transforms the message
- `Exchange` — the message object that travels through the route; contains body, headers, and properties
- `RouteBuilder` — the base class you extend to define routes

**Why Camel?** It gives you 300+ pre-built connectors (Kafka, HTTP, MongoDB, FTP, SFTP, AMQP…) without writing boilerplate. Switching from Kafka to a different message broker is mostly a config change.

---

## 4. How Kafka Pub/Sub Works

**First appears in:** `docker-compose.yml` (Kafka + Zookeeper), then all service `application.yml` files

Kafka is the event bus. Services don't call each other directly — they publish messages to **topics** and subscribe to topics.

```
PolicyIssuanceService               Platform.Compliance
        │                                   │
        │  publish: RequestComplianceCheck  │
        │ ──────────────────────────────>   │
        │  (Kafka topic)                    │
        │                                   │
        │  subscribe: ComplianceCleared     │
        │ <──────────────────────────────   │
```

**Key terms:**
- **Topic** — a named channel, like an email list (`compliance.events.compliance-cleared`)
- **Producer** — a service that publishes messages to a topic
- **Consumer** — a service that subscribes to a topic and processes messages
- **Consumer Group** — a named group of consumers; Kafka guarantees each message is processed by exactly one consumer in the group
- **Offset** — Kafka remembers where each consumer group left off, so messages are never lost

**How fan-out works:**
When `PolicyAdminSystemResponseReceived` is published, three services receive a copy:

```
Platform.Integration publishes once
        │
        ├──> policy-issuance-service   (consumer group: policy-issuance-saga)
        ├──> billing-finance-service   (consumer group: billing-finance-service)
        └──> customer-identity-service (consumer group: customer-identity-service)
```

Each service uses a **different consumer group**, so they each get their own independent copy of the same message.

---

## 5. What is a Saga?

**First appears in:** `policy-issuance-service/src/main/java/.../domain/IssuanceSagaRecord.java`

A **Saga** is a long-running workflow that coordinates multiple steps across multiple services. Unlike a database transaction (which is atomic and instant), a saga is:
- **Asynchronous** — steps happen over time (seconds to hours)
- **Distributed** — each step happens in a different service
- **Stateful** — the saga remembers where it is (persisted in MongoDB)
- **Compensatable** — if a step fails, there are explicit rollback steps

**The IssuanceSaga state machine:**

```
IssuePolicy received
    │
    ▼ Initiated
    │  → Send RequestComplianceCheck
    ▼ AwaitingCompliance
    │  → ComplianceCleared arrives
    ▼ AwaitingAccountRecord
    │  → AccountServiceRecordRetrieved arrives
    ▼ AwaitingPAS
    │  → PolicyAdminSystemResponseReceived arrives
    ▼ PASConfirmed
    │  → Start parallel: billing + customer update
    │  → BillingAssociationCreated arrives (billingComplete = true)
    │  → CustomerUpdated arrives (customerUpdateComplete = true)
    ▼ Completed   ← only when BOTH flags are true (saga join)
        → Publish PolicyIssued
```

**Why MongoDB for saga state?** Each saga step reads the current state, makes a decision, and writes the new state. MongoDB's document model is a natural fit — the whole `IssuanceSagaRecord` document is one unit.

---

## 6. What is a Content-Based Router?

**First appears in:** `platform-integration-service/src/main/java/.../routes/PasGatewayRoute.java`

A **Content-Based Router** examines an incoming message and routes it to a different destination based on the message content.

```java
from("kafka:policy.events.issue-policy-requested")
    .choice()
        .when(header("policyTypeCode").in("1","2","3","4","42","44","45","46","47"))
            .to("direct:duckcreek-commercial")
        .when(header("policyTypeCode").in("5","6","7","8","9"))
            .to("direct:duckcreek-personal")
        .when(header("policyTypeCode").in("10","12","14","17","18"))
            .to("direct:forefront")
        .otherwise()
            .to("kafka:integration.events.policy-admin-system-call-failed")
    .end();
```

**Why this matters:** `PolicyIssuanceAndLifecycleManagement` does not know or care which PAS handles the issuance. It just publishes `IssuePolicyRequested`. The Content-Based Router in `Platform.Integration` makes the routing decision. This means:

- **Adding a new PAS** = add a new `when()` clause and a new adapter route. Zero changes to the policy domain.
- **Changing which policy types go to which PAS** = config change. Zero code changes.

This is called the **Anti-Corruption Layer** pattern (principle 2.4): business domains never call external systems directly.

---

## 7. What is Idempotency and Why Does It Matter?

**First appears in:** `IssuanceSagaRoute.java` — the duplicate-detection check at the top of `saga-start` route

Kafka can deliver the same message **more than once** — this is by design for reliability. An **idempotent handler** produces the same result no matter how many times it receives the same message.

In this codebase, idempotency is implemented by checking whether the saga already exists before creating it:

```java
// If saga already exists, skip processing (idempotency check)
if (repository.existsById(issuanceId)) {
    log.warn("Duplicate IssuePolicy received — saga already exists, skipping");
    exchange.setRouteStop(true);
    return;
}
```

**Rule:** Every event handler in this system must be safe to execute more than once for the same event. Always check for an existing record by `IssuanceId` (or `CorrelationId`) before creating new state.

---

## 8. What is OpenTelemetry?

**First appears in:** `docker-compose.yml` (otel-collector service), then each service's `application.yml` (`OTEL_EXPORTER_OTLP_ENDPOINT`)

OpenTelemetry (OTel) is the standard for distributed observability. Every service emits three types of signals:

| Signal | What it captures | Where it goes |
|---|---|---|
| **Traces** | The path a request takes through all services | Grafana Tempo |
| **Logs** | Structured log lines with context | Grafana Loki |
| **Metrics** | Counters, histograms, gauges | Prometheus → Grafana |

**How it connects everything:** Every Kafka message carries a `traceparent` header (W3C standard). When a service processes a message, it reads this header to get the parent span ID and creates a child span. This means one `IssuePolicy` command produces **one distributed trace** that shows every service that touched it:

```
IssuePolicy [span: policy-issuance-service]
  └── RequestComplianceCheck [span: platform-compliance-service]
       └── RSK3X3 call [span: platform-compliance-service → rsk3x3-stub]
            └── ComplianceCleared [span: policy-issuance-service]
                 └── ... and so on
```

You can see this trace in Grafana → Explore → Tempo, and navigate to any log line from within the trace view.

---

## 9. Structured Logging (Java's Equivalent of Serilog)

**First appears in:** `logback-spring.xml` in each service

In .NET, Serilog emits JSON-structured logs. In Java, the equivalent is:
- **SLF4J** — the logging API (like `ILogger` in .NET)
- **Logback** — the implementation (like Serilog's core)
- **logstash-logback-encoder** — produces JSON output (like Serilog's JSON formatter)

```java
// In Java:
log.info("IssuanceSaga transitioned — status=Initiated issuanceId={}", issuanceId);

// Produces JSON log line:
{
  "@timestamp": "2024-11-01T10:30:00.000Z",
  "level": "INFO",
  "message": "IssuanceSaga transitioned — status=Initiated issuanceId=abc-123",
  "issuanceId": "abc-123",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "service_name": "policy-issuance-service"
}
```

**MDC (Mapped Diagnostic Context)** is how you attach contextual fields to every log line within a thread:

```java
MDC.put("issuanceId", issuanceId);  // Every log line in this thread now includes issuanceId
log.info("Processing compliance check");
log.info("Updating saga state");
MDC.clear();  // Remove when done
```

In Grafana Loki, you can then filter: `{service_name="policy-issuance-service"} | json | issuanceId="abc-123"` to see every log line for a specific issuance flow across all services.

---

## 10. Dead-Letter Queues

*(This section will be written when DLQ configuration is implemented in Phase 2)*

A **Dead-Letter Queue (DLQ)** is where messages go after all retry attempts are exhausted. It preserves the full message payload and headers (including `IssuanceId`) so an operator can replay or manually resolve the failure.

In Kafka, DLQ is typically implemented as a dedicated topic (e.g., `policy.commands.issue-policy.DLT`). Apache Camel handles this via its error handler configuration.

---

## 11. The Domain Event Contract

Every event in this system has a Java record in the `common` module:

```
common/src/main/java/com/ais/middleware/common/events/
├── policy/         ← Events published by PolicyIssuanceAndLifecycleManagement
├── compliance/     ← Events published by Platform.Compliance
├── customer/       ← Events published by CustomerIdentityAndRelationshipManagement
├── integration/    ← Events published by Platform.Integration
├── billing/        ← Events published by BillingAndFinanceManagement
└── notification/   ← Events published by Platform.Notification
```

Each record is **immutable** (Java `record` type) and serialized to/from JSON via Jackson. All services share this module — this is the contract between them. If you change an event record, all producers and consumers must be updated together.

---

## 12. Project Layout Reference

```
Middleware/
├── pom.xml                        ← Maven parent (version management)
├── common/                        ← Shared event POJOs (the "contract")
├── policy-issuance-service/       ← IssuanceSaga + REST entry point
├── platform-compliance-service/   ← Sanctions screening
├── customer-identity-service/     ← Account lookup + CRM updates
├── platform-integration-service/  ← PAS routing (Content-Based Router)
├── billing-finance-service/       ← Billing association
├── platform-notification-service/ ← Notification batching + dispatch
├── platform-ui/                   ← Next.js 15 demo + Saga Explorer
├── stubs/                         ← Fake external systems (7 Spring Boot apps)
├── observability/                 ← OTel Collector, Grafana, Loki, Tempo configs
├── docker-compose.yml             ← Full stack (run this to start everything)
└── .docs/
    ├── getting-started.md         ← This file
    └── req/                       ← Business requirements (tech-neutral)
```

**Service ports:**

| Service | Port |
|---|---|
| policy-issuance-service | 8081 |
| platform-compliance-service | 8082 |
| customer-identity-service | 8083 |
| platform-integration-service | 8084 |
| billing-finance-service | 8085 |
| platform-notification-service | 8086 |
| Platform.UI | 3000 |
| Grafana | 3001 |
| Kafka | 9092 |
| Kafdrop | 9000 |
| MongoDB | 27017 |
| Mongo Express | 8888 |
| Prometheus | 9090 |
| DuckCreek Commercial stub | 9001 |
| DuckCreek Personal stub | 9002 |
| ForeFront stub | 9003 |
| RSK3X3 stub | 9004 |
| ERM7X1 stub | 9005 |
| CRM40X1 stub | 9006 |
| CRM19X1 stub | 9007 |
