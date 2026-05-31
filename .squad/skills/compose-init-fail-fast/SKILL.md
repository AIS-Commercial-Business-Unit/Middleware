---
name: "compose-init-fail-fast"
description: "How to design docker-compose init containers so dependency chains fail at the real root cause"
domain: "devops"
confidence: "high"
source: "earned"
---

## Context
This skill applies when `docker-compose.yml` uses one-shot init services such as database seeders or topic/bootstrap jobs that other services gate on with `depends_on.condition: service_completed_successfully`.

## Patterns
- Split provisioning into steps that match resource availability boundaries; for SQL Server, create the database first, then connect with `-d <db>` for schema/table seed work.
- Make init containers exit non-zero after retry exhaustion so downstream services do not start against missing infrastructure.
- Escape shell loop variables as `$$var` inside compose command blocks so Compose does not treat them as environment substitutions.
- Keep frontend containers decoupled from backend health unless the frontend truly cannot boot without that dependency.

## Examples
- `docker-compose.yml`: `sqlserver-init` now creates `middleware_nsb`, reconnects to seed `dbo.SubscriptionRouting`, and exits `1` on failure.
- `docker-compose.yml`: `platform-ui` no longer waits on Java/.NET service health before starting.

## Anti-Patterns
- Returning `exit 0` from init containers after failed provisioning.
- Using `USE <db>` in the same fragile init batch that first creates the database and then assuming success.
- Blocking UI startup on optional backend services that are only contacted at request time.
