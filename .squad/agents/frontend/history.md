# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** React 19 / Next.js 15, TypeScript, @microsoft/signalr (Azure SignalR Service JS client), MSAL.js (Entra ID auth)
- **Key principle:** Real-time event push via Azure SignalR Service; types generated from OpenAPI specs; MSAL tokens passed to APIM, not stored in localStorage
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-31 — UC4 Full Panel Replacement (frontend-1)

- **UC4 page rewrite:** `platform-ui/src/app/uc4/page.tsx` completely replaced to showcase the two query-only workflows — GetAppraisalList (scatter-gather) and GetAppraisalDocument (content-based router).
- **Two new proxy routes:** `/api/appraisals/list/route.ts` and `/api/appraisals/document/route.ts` forward to `prs-appraisal-service:8090` with 35-second timeout (30s MQ scatter-gather + 5s buffer).
- **Quick-select UX:** List panel shows a row of policy chips (POL-001-TEST, POL-002-TEST, POL-003-TEST) for quick list demo. Document key becomes clickable from the list table, auto-filling the retrieval panel.
- **Environment config:** `PRS_APPRAISAL_SERVICE_URL` added to `.env.local` pointing to `http://localhost:8090` (dev); docker-compose overrides to `http://prs-appraisal-service:8090`.
- **Verification:** TypeScript clean (`npx tsc --noEmit` exits 0), build succeeds, both proxy routes work end-to-end with running docker stack.
- **Key learning (2026-05-31):** Clickable DocumentKey cells that auto-fill the adjacent panel reduce demo friction; users immediately see the content-based routing logic applied (RiskID vs. numeric keys route to different backend systems). The two-panel co-location makes the data flow visible without separate pages.

### 2026-05-31T16:58:30.069-04:00 — Dynamic Ops Flow Tracer (frontend)

- Updated `platform-ui/src/app/ops/[issuanceId]/page.tsx` so live mode derives participants directly from `EDA_FLOW` events instead of the old UC1-only label map.
- Added known UC4 participants (`PrsAppraisal`, `AtWork`, `Mainframe`) plus dynamic participant creation with fallback palette colors for any future flow participants.
- Preserved static UC1 reference rendering when Loki flow events are absent, while keeping live diagrams generic for UC1, UC4, and future use cases.
- Refreshed `platform-ui/src/app/ops/page.tsx` copy so the landing page describes correlation IDs, generic flow tracing, and links to both `/` and `/uc4` demos.
- Verification: `cd platform-ui && npx tsc --noEmit` and `npm run lint` both exited 0.

### 2026-05-31T16:58:30.069Z — Dynamic participant derivation + Decisions merge

- **Decision #45 (Dynamic participant derivation) merged into squad/decisions.md.** Participants now derive from live events with unknown IDs getting fallback colors, enabling generic flow diagrams across UC1, UC4, and future workflows.

- **Flow tracer patterns established:** (1) Live event stream drives participant discovery; (2) known participants pre-registered for stable colors; (3) unknown participants get runtime entries with rotating palette; (4) static UC1 fallback for demos before live logs.

- **Observable cross-checks:** TypeScript + lint clean, build passes.

### 2026-05-31T21:09:38.779-04:00 — UC4 flow tracer fan-out + saga panel (frontend)

- Added `handled` flow support end-to-end: `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` now parses `EDA_Handler`, keeps handler-level fan-out entries distinct, and suppresses redundant consumed hops when handled detail is present.
- Updated `platform-ui/src/app/ops/[issuanceId]/page.tsx` live sequence diagram to keep separate subscriber arrows, annotate handled branches, and show a left-side fan-out bracket for consecutive sibling deliveries.
- Extracted the UC1 saga card into `Uc1SagaPanel` and added a ServicePulse-style `Uc4SagaPanel` that derives timeline, current stage, sub-saga status, and key saga fields directly from live flow events when the policy saga endpoint is empty.
- Verification: `cd platform-ui && npx tsc --noEmit` and `npm run lint` both exited 0.

### 2026-05-31T21:09:38.779-04:00 — UC4 saga panel + fan-out grouping refinement (frontend-eda-flow-subscribers)

- **Saga panel derivation:** When policy saga endpoint returns null but flow contains UC4 participants + `handled` entries, render `Uc4SagaPanel` instead of empty card. Derives saga timeline directly from Loki events (scatter → distribute → handle → gather sequence).
- **Fan-out grouping:** Live diagram groups consecutive sibling `handled` entries with a left-side bracket annotation. Each subscriber arrow labeled with handler class name from `EDA_Handler` field, enabling operators to trace events to specific subscribers.
- **Flow API dedup:** Refactored `platform-ui/src/app/api/policies/[issuanceId]/flow/route.ts` dedup logic to preserve fan-out: `handled` entries never deduped, `consumed` entries deduplicated only if no corresponding `handled` entry exists for that (from, to, topic) triple.
- **UC4 detection:** `isUc4Flow()` checks for presence of UC4 participant names (`PrsAppraisal`, `AtWork`, etc.) in live events; frontend automatically switches to UC4-specific rendering without manual config.
- **Verification:** TypeScript clean, lint passed. End-to-end: backend handler-invocation logs → Loki → frontend flow API → live diagram with individual subscriber arrows.
- **Integration note:** Backend must emit JSON console logs with `EDA_Handler` at top level or in `Properties` object for platform-ui Loki parser to extract. Fallback: `EDA_Handler = "n/a"` for non-handler events (publish/consume edges).


