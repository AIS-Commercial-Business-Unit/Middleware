# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, DDD design, abstract layer, BizTalk migration patterns, scope | Architect | Domain models, service boundaries, architectural proposals, trade-off decisions |
| Apache Camel routes, Kafka topics/consumers/producers, pub/sub, message routing | Integration | Camel DSL, Kafka config, event-driven flows, message transformation |
| Azure services, APIM, App Insights, Key Vault, App Config, SignalR Service, Entra ID | Azure | Azure resource setup, APIM policies, managed identities, SignalR hub wiring |
| AKS, Kubernetes manifests, networking, Terraform/Bicep, cluster config | Platform | K8s deployments, services, ingress, infra-as-code |
| MongoDB, Java services, REST APIs, domain services | Backend | Repositories, service layer, API controllers, Java Spring/Quarkus code |
| React/Next.js, UI components, SignalR JS client, admin dashboards | Frontend | UI pages, real-time event display, form handling, dashboard components |
| Docker, docker-compose, Rancher Desktop, container builds, CI/CD pipelines | DevOps | Dockerfiles, compose files, build pipelines, container networking |
| Grafana dashboards, metrics pipelines, alerting rules | Grafana | Dashboard JSON, Prometheus scrape configs, alert thresholds |
| Code review | Architect | Review PRs, check quality, DDD compliance |
| Testing, log verification, test evidence | QA | Integration tests, Serilog output validation, test reports |
| Scope & priorities | Architect | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
