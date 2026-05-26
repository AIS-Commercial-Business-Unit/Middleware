# Getting Started — Java Stack

This guide walks you through running and understanding the **Apache Camel / Java** side of the AIS Middleware Platform.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [Rancher Desktop](https://rancherdesktop.io/) or Docker Desktop | Latest | Container runtime |
| Java 21 (JDK) | 21 LTS | For local builds outside Docker |
| Maven | 3.9+ | For local builds outside Docker |
| PowerShell | 7+ | For running test scripts |

> **Tip:** All services can be built and run entirely inside Docker — you don't need Java or Maven installed locally to run the demos.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Integration framework | **Apache Camel 4.8** |
| Application container | **Spring Boot 3.3 / Java 21** |
| Messaging | **Kafka** (via camel-kafka component) |
| Domain persistence | **MongoDB** (Spring Data MongoDB) |
| Structured logging | **SLF4J + Logback** with `logstash-logback-encoder` (JSON output) |
| Distributed tracing | **OpenTelemetry** → Grafana Tempo |
| Metrics | **Micrometer** → Prometheus → Grafana |

---

## Project Layout

```
java/
├── pom.xml                          ← Maven parent POM (manages all versions)
├── common/                          ← Shared domain event POJOs + Kafka config
├── policy-issuance-service/         ← UC1: IssuanceSaga + REST entry point (port 8081)
├── platform-compliance-service/     ← Sanctions screening service (port 8082)
├── customer-identity-service/       ← Account lookup + CRM update (port 8083)
├── platform-integration-service/   ← PAS routing (Content-Based Router) (port 8084)
├── billing-finance-service/         ← Billing association (port 8085)
├── platform-notification-service/  ← Notification batching + dispatch (port 8086)
└── stubs/                           ← 7 lightweight fake external systems
    ├── duckcreek-commercial-stub/   ← port 9001
    ├── duckcreek-personal-stub/     ← port 9002
    ├── forefront-stub/              ← port 9003
    ├── rsk3x3-compliance-stub/      ← port 9004
    ├── erm7x1-account-stub/         ← port 9005
    ├── crm40x1-customer-stub/       ← port 9006
    └── crm19x1-billing-stub/        ← port 9007
```

---

## Running the Java Stack

### Option 1: Docker Compose (Recommended)

```bash
# From repository root — starts Java services + all infrastructure
docker compose up --build

# Start only Java services (skip .NET)
docker compose up --build \
  kafka zookeeper mongodb mongo-express \
  otel-collector grafana loki prometheus tempo \
  duckcreek-commercial-stub duckcreek-personal-stub forefront-stub \
  rsk3x3-stub erm7x1-stub crm40x1-stub crm19x1-stub \
  policy-issuance-service platform-compliance-service \
  customer-identity-service platform-integration-service \
  billing-finance-service platform-notification-service \
  platform-ui
```

### Option 2: Local Maven Build

```bash
cd java
mvn clean install -DskipTests

# Run a single service locally (infrastructure must be running in Docker)
cd policy-issuance-service
mvn spring-boot:run
```

---

## Verifying the Stack Is Running

```bash
# Check all containers are healthy
docker compose ps

# Test UC1 — Policy Issuance (Java backend)
curl -X POST http://localhost:3000/api/backend-info
# Should return: {"backend":"java"}

curl -X POST http://localhost:8081/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":"Test User","coverageAmount":100000}'
```

---

## Key Concepts

### Apache Camel Route

A Camel Route is a pipeline: `from(source) → process → to(destination)`.

```java
from("kafka:compliance.events.compliance-cleared?groupId=policy-issuance-saga")
    .process(exchange -> {
        // Deserialize event, update saga state, build next command
    })
    .to("kafka:customer.commands.get-or-create-account-record");
```

**Why Camel?** 300+ pre-built connectors. Switching the message broker is a config change, not a rewrite.

### Saga Pattern

`IssuanceSaga` is a long-running stateful workflow persisted in MongoDB. State transitions:

```
Initiated → AwaitingCompliance → AwaitingAccountRecord →
AwaitingPAS → PASConfirmed → Completed
```

Each step: read state from MongoDB → process event → write updated state → publish next command.

### Content-Based Router

`platform-integration-service` routes `IssuePolicyRequested` events to the correct Policy Admin System (DuckCreek Commercial, DuckCreek Personal, or ForeFront) based on `policyTypeCode`. The domain service never knows which PAS is used.

### Structured Logging

Every service logs JSON to stdout → Loki → Grafana. To filter by a specific issuance:

```
# In Grafana → Explore → Loki:
{service_name="policy-issuance-service"} | json | issuanceId="<your-id>"
```

---

## Observability

| Signal | Where |
|---|---|
| Structured logs | Grafana → Explore → Loki |
| Distributed traces | Grafana → Explore → Tempo |
| Metrics dashboards | Grafana → Dashboards |
| Kafka topics | http://localhost:9000 (Kafdrop) |
| MongoDB documents | http://localhost:8888 (Mongo Express) |

**Trace correlation:** Every Kafka message carries a `traceparent` W3C header. One `IssuePolicy` request produces one distributed trace spanning all 6 services — visible in Tempo.

---

## Running Unit Tests

```bash
cd java
mvn test

# Run tests for a single service
cd java/policy-issuance-service
mvn test
```

> **Note:** Apache Camel routes require more setup for unit testing than NServiceBus handlers. See [Testing Comparison](.docs/testing-comparison.md) for details.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Service fails to connect to Kafka | Wait 30s after `docker compose up` — Kafka takes time to initialize |
| Saga stuck in `AwaitingCompliance` | Check `rsk3x3-stub` is running: `docker compose ps rsk3x3-stub` |
| No logs in Grafana Loki | Check `otel-collector` container is healthy |
| MongoDB connection refused | Ensure `mongodb` container is healthy: `docker compose ps mongodb` |

---

## Further Reading

- [Running the Demos](.docs/running-the-demos.md) — what UC1 and UC3 demonstrate
- [Full Getting Started Concepts Guide](.docs/getting-started.md) — deep-dive on Camel, Kafka, Sagas
- [Testing Comparison](.docs/testing-comparison.md) — Apache Camel vs NServiceBus testability
