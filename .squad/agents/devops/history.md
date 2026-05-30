# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Docker, docker-compose (Rancher Desktop compatible), GitHub Actions, Azure Container Registry (ACR)
- **Key principle:** docker compose up brings the full stack locally; multi-stage Dockerfiles; every service has a health check; no 'latest' tags in prod
- **Local dev:** Rancher Desktop (Docker) — all compose files verified against it
- **Created:** 2026-05-25

## Learnings

### Summary of Prior Work (2026-05-25 to 2026-05-29)

**2026-05-25 UC3:** Added `platform-file-processing-service` to Maven reactor, configured `renewal-data` named volume for persistent file storage.

**2026-05-25 Architecture Sweep:** Comprehensive hardening of all 35+ services — added healthchecks to every service, kafka-setup pre-creates 24 topics (3 partitions each), mem_limit applied to all services (256m–1g), non-root `appuser` added to all 14 Java services, created 15 .dockerignore files, environment-variable configuration for Grafana admin password. Result: 7 critical security & operations gaps fixed, all services start in correct order.

**2026-05-27:** Fixed `platform-ui` hard dependency on backend (decoupled startup), repaired SQL initialization pattern (split create-then-seed), verified all .NET services healthy. Fixed renewal drop-zone bind mounts (repo-local `.docker-data/renewals/java` and `.docker-data/renewals/dotnet`), resolved non-root permission failures.

**2026-05-29 UC4 Stack-up:** 
- Bug 1: All 14 non-prs Java Dockerfiles missing `prs-appraisal-service/pom.xml` COPY step — root cause was Maven reactor module addition without updating dependent Dockerfiles. Fix: added COPY to all services. Rule established: every new maven module requires all-Dockerfile patching.
- Bug 2: `ProcessAppraisalStatusUpdateCommand.cs` missing `ICommand` interface — caused NServiceBus startup crash. Fix: added using directive and interface implementation.
- Outcome: 34/37 services healthy on first stack run (3 UC4 services new).

**2026-05-29 Health Audits:**
- mongo-express FailingStreak 644: BusyBox wget localhost→IPv6 loopback, service binds IPv4. Fix: 127.0.0.1 (explicit IPv4). Decision #40 established.
- dotnet-platform-integration stuck "Created": orphaned container from partial prior start. Fix: `docker compose up -d <service>`.
- otel-collector distroless: no health check possible. Fix: CMD health check using `/otelcol-contrib --help` as process-alive probe.

**2026-05-29 Resource Pressure Crisis:**
- Diagnosed 4 infra services at critical OOM risk: tempo (97%), kafka (88%), zookeeper (86%), promtail (67%). Symptom: "containers up and down", PRS Appraisal timeout/comeback.
- Root cause: Tempo block compaction (in-memory) at 97% caused GC surge → otel-collector buffer pressure → services spending extra flush time → perceived timeout.
- Fixes applied:
  - tempo: mem_limit 512m→768m + GOMEMLIMIT=650MiB (Go soft limit signals GC before hard OOM)
  - kafka: mem_limit 1g→1.5g + KAFKA_HEAP_OPTS="-Xms256m -Xmx768m" (explicitly bounds JVM)
  - zookeeper: mem_limit 256m→384m + ZOOKEEPER_HEAP_SIZE=256 (bounds heap within container)
  - promtail: mem_limit 128m→256m (doubled for log volume spikes)
- Outcome: all 37 containers healthy, stable memory profiles (tempo 20%, kafka 34%, zookeeper 28%, promtail 20%).

### Key Rules Established (2026-05-29)

1. **Dockerfile POM Sync Rule:** Every new module added to `java/pom.xml` requires `COPY {module}/pom.xml {module}/` line in ALL 14 Java service Dockerfiles (in POM-copy block before `RUN mvn dependency:go-offline`). Missing module causes hard build failure. Decision #32.

2. **BusyBox Health Check Rule:** All BusyBox wget health checks must use `127.0.0.1` (explicit IPv4), never `localhost`. IPv6 localhost resolution is default in Linux network stacks; services binding IPv4-only fail health checks. Decision #40.

3. **JVM Memory Bounds Rule:** All JVM containers (Kafka, Kafdrop, Zookeeper, Java services) must set explicit `-Xms`/`-Xmx`. Rule of thumb: `Xmx` ≤ 50% of container `mem_limit`. Reserve remaining 50% for non-heap (metaspace, OS, NIO, native threads). Kafdrop minimum: 384m limit + Xmx128M. Kafka minimum: 1.5g limit + Xms256m Xmx768m.

4. **Go Container Rule:** All Go containers (Tempo, Loki, Prometheus, Grafana) should set `GOMEMLIMIT` ≈ 85% of mem_limit. Go GC 1.19+ respects this soft ceiling and triggers aggressive collection before hard OOM.

5. **Steady-State Target:** No container should exceed 60% of its mem_limit during normal operation. If `docker stats --no-stream` shows ≥75%, that service needs a limit increase before next demo/load test.

6. **Startup Timing:** NServiceBus SQL persistence auto-installs on first run; health checks may fire during table creation. 60s start_period + 3 retries × 30s interval design handles transient startup races correctly. Give stack 3 min before investigating "unhealthy" containers during cold starts.

### 2026-05-30 — UC4 docker-compose wiring + Maven reactor POM sync

**Completed:** 
- `java/pom.xml` added `<module>stubs/deipde07-mq-simulator</module>`
- All 15 Java Dockerfiles patched with `COPY stubs/deipde07-mq-simulator/pom.xml stubs/deipde07-mq-simulator/`
- `docker-compose.yml` wired:
  - `activemq-artemis` service (apache/activemq-artemis:2.37.0, mem_limit 384m, curl health on 127.0.0.1:8161)
  - `deipde07-mq-simulator` service (custom Java build, port 9020, depends_on artemis healthy, wget health on 127.0.0.1:9020, mem_limit 256m)
  - `prs-appraisal-service` updated: depends_on artemis + simulator with service_healthy, added ARTEMIS_* env vars, added JAVA_TOOL_OPTIONS="-Xmx256m" (50% of 512m limit)

**Decisions applied:**
- BusyBox health checks use 127.0.0.1 per Decision #40
- JAVA_TOOL_OPTIONS Xmx = 50% of mem_limit per JVM rule
- All services on middleware-net bridge (consistent with stack)
- deipde07-mq-simulator positioned in EXTERNAL SYSTEM STUBS section (stub, not domain service)
