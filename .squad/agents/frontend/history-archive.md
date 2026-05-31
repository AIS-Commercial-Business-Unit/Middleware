# Frontend History Archive

**Archived:** 2026-05-31T20:44:53Z  
**Summary:** Learnings from May 25-30 consolidated into archive.

## Period: 2026-05-25 to 2026-05-30

### Highlights

**UC3 File Processing Control Plane (2026-05-25):**
- Next.js 15 catch-all API routes require Promise-based params
- SWR refreshInterval function stops polling at terminal states
- Inline types per page (not shared files) reduces bundle size
- Batch progress bar colors: blue=Processing, green=Completed, orange=PartialFailure, red=Failed

**Demo Shell Pattern (2026-05-28):**
- Proxy API routes gracefully fall back to mock data when backend unavailable
- Demo gap visibility is first-class UI concern with badges and expandable requirement panels
- UC4 appraisal page scaffolded with `/api/riskid/*` stubs

**Live Sequence Diagrams (2026-05-27):**
- Sequence diagram SVG renders shapes; HTML overlay renders tooltips
- FlowEventDetails enriched in Loki proxy response; copy centralized
- Dynamic flow mode falls back to static topology when no live events

**Flow Diagram Bug Fixes (2026-05-27):**
- Dedup key `messageType|from|to` (removed `direction` to eliminate double-arrows)
- TOPIC_TO_CONSUMER mappings corrected (policy-issued → Notification, not self-loop)
- HOSTNAME=0.0.0.0 required for Next.js standalone IPv4 binding in Alpine

**Runtime Backend Switching (2026-05-27):**
- Cookie-backed `/api/backend` route enables runtime stack selection
- `BackendSwitcher` client island with window event for nav sync

**Demo Control Panel (2026-05-29):**
- Health aggregator `/api/demo/health` fans out to 21 services in parallel (3s timeout per)
- Three mutation routes (reset, seed, clear) with mock fallbacks
- Progress animation + status log with scrollable view

**Build Reliability Pattern (2026-05-29):**
- Next.js lint/TypeScript errors silently kill new routes in standalone build
- Check `npm run build` success before debugging missing routes in Docker
- ESLint bare `<a>` tag rule violation causes full app build failure

### Key Takeaways

- Frontend demo readiness depends on backend service availability — mock fallbacks enable early staging
- Next.js standalone Docker builds require lint/type clean for any new routes to be included
- Live observability (Loki-backed sequence diagrams) validates architecture semantics operationally
- Backward compatibility: `/api/demo/health` works entirely in Next.js layer (no backend change needed)
