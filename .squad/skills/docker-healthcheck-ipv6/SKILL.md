---
name: "docker-healthcheck-ipv6"
description: "Avoid IPv6 resolution failures in Docker health checks by using 127.0.0.1 instead of localhost in BusyBox containers"
domain: "infrastructure, docker, health-checks"
confidence: "high"
source: "earned — diagnosed mongo-express FailingStreak 644 (2026-05-29)"
---

## Context

Applies any time you add a `healthcheck` to a Docker service that uses a BusyBox-based image
(Alpine, mongo-express, Kafka images, etc.) where the health probe uses `wget` to call the
service's own HTTP endpoint.

Also applies to distroless images where no HTTP client is available at all.

## Patterns

### BusyBox wget — always use 127.0.0.1

Modern Linux network stacks inside Docker containers resolve `localhost` to `::1` (IPv6
loopback) by default. If the service binds to `0.0.0.0` (IPv4), the IPv6 request fails with
"Connection refused" even though the service is fully healthy.

```yaml
# CORRECT
healthcheck:
  test: ["CMD-SHELL", "wget -qO- http://127.0.0.1:<port>/<path> > /dev/null 2>&1"]
  interval: 15s
  timeout: 5s
  retries: 3
  start_period: 20s
```

### Distroless images — process-alive probe via container binary

Distroless images (e.g., `otel/opentelemetry-collector-contrib`) have no shell, no wget,
no curl. The only available binary is the container's main executable. Use `--help` as a
no-op probe that exits 0 if the process is alive.

```yaml
# otel-collector example
healthcheck:
  test: ["CMD", "/otelcol-contrib", "--help"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 15s
```

**Note:** This is a process-alive check only — it does not verify the service is actually
processing data. Use `condition: service_started` in dependent services for distroless containers
where no real HTTP health check is possible.

### Container stuck in "Created" state

If `docker ps -a` shows a container as "Created" but `docker compose ps` lists it as absent
or not started, the container was orphaned by a partial compose start. Recover with:

```powershell
docker compose up -d <service-name>
```

All dependencies are re-evaluated; the container starts immediately if deps are healthy.

## Examples

From this project's `docker-compose.yml`:

```yaml
# mongo-express — BusyBox wget, IPv4 fix
healthcheck:
  test: ["CMD-SHELL", "wget -qO- http://127.0.0.1:8081/ > /dev/null 2>&1"]
  interval: 15s
  timeout: 5s
  retries: 3
  start_period: 20s

# otel-collector — distroless process-alive probe
healthcheck:
  test: ["CMD", "/otelcol-contrib", "--help"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 15s
```

## Anti-Patterns

- `wget -qO- http://localhost:<port>/` in BusyBox containers — silently fails forever (FailingStreak accumulates without any error output)
- No health check on a distroless container — container shows no `(healthy)` badge, breaks any downstream `condition: service_healthy` dependencies
- Using `curl` in images that only have BusyBox (no curl binary)
- Assuming `localhost` is `127.0.0.1` universally — only safe in glibc-based images
