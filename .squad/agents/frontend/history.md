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
