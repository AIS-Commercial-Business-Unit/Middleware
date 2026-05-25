# Copilot Instructions — Middleware

## Repository Overview

This is a **Java-based middleware platform** replacing BizTalk, built on Apache Camel, Kafka, MongoDB, and Azure. It uses the **Squad AI team framework** for agent-coordinated development (`.squad/` directory).

**Stack:** Apache Camel · Kafka · MongoDB · Grafana · Prometheus  
**Azure:** AKS · APIM · App Insights · Azure Monitor · Key Vault · App Configuration · Blob Storage · Azure SignalR Service · Entra ID Managed Identities  
**Local dev:** Docker (Rancher Desktop) — `docker compose up` starts the full stack  
**Frontend:** React 19 / Next.js 15 · TypeScript · Azure SignalR JS client  
**Logging:** SLF4J + structured logging (Serilog-equivalent for Java)

## Architecture Principles

- **Abstract layer is non-negotiable:** Domain layer must not import any infrastructure type (Kafka, MongoDB, Spring, etc.). Adapters implement domain interfaces.
- **Event-driven, pub/sub:** Services communicate via Kafka topics. Synchronous REST only crosses service boundaries via APIM.
- **DDD:** Domain entities, aggregates, value objects, and bounded contexts defined before implementation.
- **Stack-portable:** If the underlying technology changes, only the adapter layer changes — not the domain or application layers.

## Squad Framework

Before starting any issue work, read these files:

1. `.squad/team.md` — roster, member roles, routing
2. `.squad/decisions.md` — team decisions to respect
3. If the issue has a `squad:{member}` label, read `.squad/agents/{member}/charter.md`

### Issue Assignment

- `squad` — untriaged; Lead (Architect) triages and assigns a `squad:{member}` sub-label
- `squad:{member}` — assigned; that member picks it up

### Decision Drop-Box

Write team-relevant decisions to `.squad/decisions/inbox/{your-name}-{brief-slug}.md`. Do **not** write directly to `decisions.md` — Scribe merges the inbox.

## Git Workflow

**All feature work branches from `dev`, not `main`.**

| Branch | Purpose |
|--------|---------|
| `main` | Released, stable only |
| `dev` | Integration branch — all PRs target this |

### Branch Naming

```
squad/{issue-number}-{kebab-case-slug}
```

### Workflow

```bash
git checkout dev && git pull origin dev
git checkout -b squad/{issue-number}-{slug}
# do the work
git push -u origin squad/{issue-number}-{slug}
gh pr create --base dev --title "{description}" --body "Closes #{issue-number}"
```

## Key Conventions

### Test Discipline

Update tests in the same commit as API changes. Never leave test assertions stale:
- Changed function signature → update corresponding tests before committing
- Test evidence required: inputs + expected output + actual output + relevant log lines

### Structured Logging

Every service method logs entry and exit with correlation ID. Assertions in integration tests verify that expected structured log entries fired with correct properties.

### Local Run Verification

Before any PR: `docker compose up`, run integration tests, verify logs match expected output. "Works on my machine" requires log evidence.

### `.squad/` Files — Append-Only

`decisions.md`, `agents/*/history.md`, `log/**`, and `orchestration-log/**` are **append-only**. Never rewrite or reorder these files. `.gitattributes` uses `merge=union` on these paths.

### Capability Self-Check

Before starting work, check your capability profile in `.squad/team.md`:
- 🟢 Good fit → proceed autonomously
- 🟡 Needs review → proceed, note in PR
- 🔴 Not suitable → comment on issue, suggest reassignment
