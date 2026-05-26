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

