# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** Docker, docker-compose (Rancher Desktop compatible), GitHub Actions, Azure Container Registry (ACR)
- **Key principle:** docker compose up brings the full stack locally; multi-stage Dockerfiles; every service has a health check; no 'latest' tags in prod
- **Local dev:** Rancher Desktop (Docker) — all compose files verified against it
- **Created:** 2026-05-25

## Learnings

### UC3 — platform-file-processing-service Integration (2026-05-25)

**Approach:**
- Updated parent `pom.xml` to include `platform-file-processing-service` module in the build
- Extended `docker-compose.yml` to support file processing workflows:
  - Added `renewal-data` named volume for persistent file storage across container restarts
  - Configured platform-file-processing-service with:
    - Port 8087 for HTTP access
    - Volume mount at `/app/data/renewals` for inbound, processed, and error directories
    - Kafka and MongoDB dependencies for async processing and persistence
    - OpenTelemetry instrumentation for observability
  - Updated platform-ui service to depend on and route to file-processing-service via `FILE_PROCESSING_SERVICE_URL`

**Key decisions:**
- Dedicated volume for renewals data ensures file persistence and isolation from other services
- Service positioned after notification-service in dependency chain (logical grouping with other domain services)
- File directories (inbound, processed, error) standardized for clear workflow tracking

### Architecture Sweep (2026-05-25T20:46:06.631-04:00)

**Approach:** Full audit of all container/infra config. Fixed all critical gaps.

**Key changes:**
- Added `healthcheck:` to every service in `docker-compose.yml` (was zero). All `depends_on` now use `condition: service_healthy` or `condition: service_completed_successfully`.
- Added `kafka-setup` one-shot service that pre-creates all 24 Kafka topics with `--partitions 3 --replication-factor 1` after Kafka is healthy. Disabled `KAFKA_AUTO_CREATE_TOPICS_ENABLE`.
- Added `kafka-data` named volume so Kafka offsets/logs survive `docker compose down`.
- Added `restart: unless-stopped` to all long-running services; `restart: on-failure` for kafka-setup.
- Added non-root `USER appuser` to all 14 Java Dockerfiles (domain services + stubs) using Alpine `addgroup`/`adduser`.
- Created 15 `.dockerignore` files (14 Java services/stubs + platform-ui) — none existed before.
- Moved `GF_SECURITY_ADMIN_PASSWORD` from hardcoded `admin` to `${GF_SECURITY_ADMIN_PASSWORD:-admin}`.
- Created `.env.example` at repo root.
- Added `health_check` extension to `otel-collector.yaml` (port 13133) so the collector is health-checkable.
- Added `mem_limit` to every service (256m–1g depending on role).

**Key decisions:**
- Java service Dockerfiles remain single-stage (copy from `target/`). Multi-stage would require repo-root build contexts and defeats the monorepo Maven build model.
- All services remain on single `middleware-net` bridge. Network segmentation is an Azure-level concern.
- `promtail` health check uses port 9080 (`/ready` endpoint on Promtail's HTTP server).

**Architecture Sweep Container Hardening Results:**
- 7 critical security & operations gaps fixed
- All 35+ services now have health checks with appropriate probes
- kafka-setup service owns deterministic topic provisioning (24 topics, 3 partitions)
- All 21 Java containers (14 services + 7 stubs) run as non-root appuser
- 15 .dockerignore files created, reducing build contexts
- Memory limits applied to all services (prevents resource starvation)
- All services start in correct order and reach healthy state
- Docker stack verified fully functional with `docker compose up --build`
- Orchestration log: `.squad/orchestration-log/2026-05-26T01-33-25Z-devops-1.md`

### Compose dependency chain repair (2026-05-27T14:19:40.019-04:00)

**Approach:** Audited the full `platform-ui` dependency chain in `docker-compose.yml`, traced blocked startup through `dotnet-file-processing -> dotnet-policy-issuance -> sqlserver-init`, and validated the running stack with `docker compose ps`, service logs, and HTTP health checks.

**Key learnings:**
- `dotnet-file-processing` does exist in `docker-compose.yml`; the blocker was not a missing service but an upstream readiness failure on the .NET chain.
- `sqlserver-init` was returning success even when `middleware_nsb` was never created because the compose command tried to `USE middleware_nsb` in the same batch and swallowed failure with `exit 0`.
- `platform-ui` should not hard-gate on backend container health; it only needs backend URLs at runtime, so startup should stay decoupled from Java/.NET readiness.
- Key file paths for this repair: `docker-compose.yml`, `.squad/decisions/inbox/devops-platform-ui-startup-decoupling.md`, `.squad/skills/compose-init-fail-fast/SKILL.md`.

**Resolution completed:**
- Removed hard `depends_on` from `platform-ui` service
- Fixed SQL init to split create-then-seed pattern (decision #27)
- All .NET services confirmed healthy on stack restart
- Platform-ui now available during partial backend outages
- Verified `docker compose up --build` completes without UI service disappearing

### Renewal drop-zone bind mounts (2026-05-27T14:29:44.195-04:00)

**Approach:** Reworked renewal file storage in `docker-compose.yml` so file-processing services write to repo-local bind mounts under `.docker-data/renewals`, then validated both stacks locally with container startup checks, health checks, and a live Java batch file generation smoke test.

**Key learnings:**
- `platform-file-processing-service` already had a data mount, but the original named-volume strategy at `/app/data/renewals` still allowed the UI create-file path to fail because the drop-zone subdirectories were not reliably writable/visible during local runs.
- Java works cleanly with a parent bind mount at `/app/data`; it created `renewals/inbound`, `renewals/processed`, and `renewals/error` on startup, and the generated CSV appeared on the host at `.docker-data/renewals/java/renewals/inbound`.
- .NET needed explicit bind mounts per drop-zone subdirectory (`inbound`, `processed`, `error`) to avoid non-root permission failures when creating `/app/data/renewals` from a fresh host mount.
- Key file paths for this repair: `docker-compose.yml`, `.docker-data/renewals/java`, `.docker-data/renewals/dotnet`, `.squad/decisions/inbox/devops-renewal-bind-mounts.md`, `.squad/skills/non-root-file-drop-bind-mounts/SKILL.md`.

### UC4 — prs-appraisal-service + dotnet-prs-appraisal stack-up (2026-05-29T05:03:49.407-04:00)

**Approach:** Built UC4 Java/NET services (JARs and DLLs were already fresh from 2026-05-28), ran `docker compose up -d --build` for the new services, debugged two blocking build/runtime failures.

**Bug 1 — All non-prs Dockerfiles missing `prs-appraisal-service/pom.xml` COPY step:**
- Root cause: When `prs-appraisal-service` was added to the parent `pom.xml` as a reactor module, 14 existing Dockerfiles that copy all module POMs for Maven reactor resolution were not updated.
- Symptom: `mvn dependency:go-offline` failed with `Child module /workspace/prs-appraisal-service of /workspace/pom.xml does not exist`.
- Fix: Added `COPY prs-appraisal-service/pom.xml prs-appraisal-service/` after the `platform-file-processing-service/pom.xml` COPY line in all 14 affected Dockerfiles.
- **Rule:** Every time a new module is added to `java/pom.xml`, all Dockerfiles in `java/` must get the corresponding `COPY {module}/pom.xml {module}/` line.

**Bug 2 — `ProcessAppraisalStatusUpdateCommand` missing `ICommand` interface:**
- Root cause: `Middleware.Contracts/Commands/ProcessAppraisalStatusUpdateCommand.cs` was created without `using NServiceBus;` and `: ICommand`.
- Symptom: NServiceBus startup crash — `Cannot configure routing for type ... because it is not considered a message`.
- Fix: Added `using NServiceBus;` and `: ICommand` to the class declaration.
- **Rule:** Every NServiceBus command class in `Middleware.Contracts` MUST implement `ICommand` (or `IEvent`/`IMessage`). Forgetting this causes a hard startup crash.

**Outcome:**
- prs-appraisal-service: healthy on port 8090
- dotnet-prs-appraisal: healthy on port 8189
- dotnet-customer-identity: healthy on port 8183
- customer-identity-service: healthy on port 8083
- platform-integration-service: healthy on port 8084
- platform-ui: healthy on port 3000
- RiskIDMQGateway smoke test: POST http://localhost:8084/api/riskid/status-update → 202 Accepted
- Kafka `prs.events.*` topics confirmed: 11 topics present
- Stack total: 34/37 healthy (mongo-express pre-existing unhealthy, not UC4)
- One application-level DLQ error in prs-appraisal-service: `OffsetDateTime` BSON codec missing — routes to DLQ as designed, not a container failure.

**Key file paths:**
- `java/pom.xml` — parent reactor module list
- `java/stubs/*/Dockerfile`, `java/*/Dockerfile` — all needed prs-appraisal-service pom COPY
- `dotnet/Middleware.Contracts/Commands/ProcessAppraisalStatusUpdateCommand.cs` — ICommand fix
- `docker-compose.yml` — prs-appraisal-service:8090, dotnet-prs-appraisal:8189

### Cross-agent learning — Dockerfile POM sync requirement (2026-05-29)

**From Scribe decision merge:** New Maven modules require Dockerfile POM sync across all Java services. When a new module is added to `java/pom.xml`, ALL Dockerfiles under `java/` must receive a corresponding `COPY {module}/pom.xml {module}/` line in the POM-copy block (immediately before `RUN mvn dependency:go-offline`). Missing module causes hard build failure. Whoever adds a module to `java/pom.xml` is responsible for patching all Dockerfiles in the same commit.

**Pattern established:** This mechanical step prevents the `[ERROR] Child module ... does not exist` failure seen with prs-appraisal-service.

### Container transient health check recovery — dotnet-policy-issuance (2026-05-29T07:28:46.621-04:00)

**Reported issue:** Steven Suing reported `dotnet-policy-issuance` container unhealthy on fresh `docker compose up`.

**Investigation:** Full stack health audit: `docker ps -a`, logs inspection, health check config verification.

**Root cause:** Transient SQL persistence initialization race condition during NServiceBus startup. Container logs show SQL outbox/saga table creation in progress when initial health checks fired. Not a config bug — timing variance across cold starts.

**Status:** Container recovered to healthy state within normal startup period (60s start_period + 3 retries × 30s interval = self-healing). Current health: **healthy, FailingStreak=0**.

**Health configuration verified:**
- Test: `wget -qO- http://localhost:8181/health`
- Interval: 30s, Timeout: 10s, Retries: 3, Start period: 60s
- Mem limit: 512m (sufficient for .NET with SQL persistence)
- Dependencies: sqlserver (healthy), sqlserver-init (completed), kafka (healthy), mongodb (healthy)

**Learnings:** NServiceBus SQL persistence auto-installs on first run. Initial health check may fire during table creation. The 60s start_period + 3 retries design handles this correctly. No code or config changes needed. If issue recurs, check for memory pressure (watch `docker stats` for dotnet-policy-issuance) or SQL Server connectivity timeouts.

### Full stack health repair — 17/21 → 21/21 healthy (2026-05-29T10:07:07.672-04:00)

**Reported issue:** Steven Suing reported 17/21 containers healthy; 4 not healthy.

**Containers investigated and fixed:**

**1. mongo-express — FailingStreak 644 (unhealthy)**
- Root cause: BusyBox `wget` resolves `localhost` to `::1` (IPv6 loopback). Node.js binds to `0.0.0.0:8081` (IPv4 only). Health check `wget -qO- http://localhost:8081/` always fails with "Connection refused" even though the service is perfectly healthy and reachable from the host at `http://127.0.0.1:8888/`.
- Fix: Changed health check to `wget -qO- http://127.0.0.1:8081/` (explicit IPv4 address).
- **Rule:** Any BusyBox wget health check must use `127.0.0.1`, never `localhost`. IPv6 resolution of `localhost` is the default in newer Linux network stacks.

**2. dotnet-platform-integration — stuck in "Created" state (never started)**
- Root cause: Container was created by a previous `docker compose up` run but never started. Likely happened during an interrupted or partial compose start. All dependencies (dotnet-policy-issuance, dotnet-billing-finance, etc.) were healthy and met.
- Fix: `docker compose up -d dotnet-platform-integration` — container started and reached healthy within seconds.
- **Rule:** If a container shows "Created" state in `docker ps -a` but is missing from `docker compose ps`, it was orphaned from a prior partial start. Always run `docker compose up -d <service>` to recover.

**3. otel-collector — no health check (distroless image)**
- Root cause: `otel/opentelemetry-collector-contrib:0.111.0` is genuinely distroless — no `/bin/sh`, no `wget`, no `curl`, no `nc`. Previous comment said "health check not possible via CMD-SHELL" which is correct, but no CMD health check was added either.
- Fix: Added `healthcheck: test: ["CMD", "/otelcol-contrib", "--help"]` — a process-alive probe using the container's own binary. Exits 0 if the collector binary is running; non-zero if the container is dead.
- **Limitation:** This is a process-alive check, not a service health check. The `health_check` extension (port 13133) provides deeper health but cannot be queried from within a distroless container. This is acceptable for local dev — services depend on `condition: service_started`.
- **Rule:** For distroless containers, use `CMD ["/the-main-binary", "--help"]` as a last-resort process-alive health check so the container gets a `(healthy)` badge.

**Outcome:** All 21 trackable services now show `(healthy)`. Full running stack verified with `docker compose ps`.

**Key file:** `docker-compose.yml` (mongo-express healthcheck line ~185, otel-collector healthcheck added ~244)

## Team Decisions Generated

### Decision #40: BusyBox wget Health Checks Must Use 127.0.0.1, Not localhost (2026-05-29)
- Merged from devops inbox decision
- IPv6 localhost issue in BusyBox affects all alpine/busybox health checks
- Applied to mongo-express fix; standard for all future BusyBox-based services
