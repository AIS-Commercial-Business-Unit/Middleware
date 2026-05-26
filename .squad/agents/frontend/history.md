# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** React 19 / Next.js 15, TypeScript, @microsoft/signalr (Azure SignalR Service JS client), MSAL.js (Entra ID auth)
- **Key principle:** Real-time event push via Azure SignalR Service; types generated from OpenAPI specs; MSAL tokens passed to APIM, not stored in localStorage
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-25 — UC3 File Processing Control Plane

- Next.js 15 catch-all API routes (`[...path]`) require `params` to be awaited as a `Promise<{ path: string[] }>` — do NOT destructure synchronously.
- SWR `refreshInterval` accepts a function `(data) => number` — use this to stop polling when the resource reaches a terminal state (Completed, PartialFailure, Failed, TimedOut).
- Inline TypeScript types per page (not shared type files) keeps each page self-contained and avoids import overhead — matches the existing codebase style.
- When proxying to upstream services, use `next: { revalidate: 0 }` on GET fetches to prevent Next.js from caching live status data.
- Progress bar color should reflect the batch outcome: blue=Processing, green=Completed, orange=PartialFailure, red=Failed.

