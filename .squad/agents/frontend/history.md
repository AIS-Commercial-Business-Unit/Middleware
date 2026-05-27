# Project Context

- **Owner:** Steven Suing
- **Project:** Middleware — Apache Camel/Kafka/MongoDB/Azure platform replacing BizTalk
- **Stack:** React 19 / Next.js 15, TypeScript, @microsoft/signalr (Azure SignalR Service JS client), MSAL.js (Entra ID auth)
- **Key principle:** Real-time event push via Azure SignalR Service; types generated from OpenAPI specs; MSAL tokens passed to APIM, not stored in localStorage
- **Local dev:** Rancher Desktop (Docker)
- **Created:** 2026-05-25

## Learnings

<!-- Append new learnings below. -->

### 2026-05-27 — Sequence diagram hover tooltips

- **Sequence-diagram hover UX works best when the SVG stays purely for shapes and the tooltip is rendered as an absolutely positioned HTML overlay inside a relatively positioned wrapper.** This keeps the arrow hit area simple, allows Tailwind styling, and avoids SVG text layout constraints for multi-line event details.

- **Live `FlowEvent` records should preserve a richer `details` object from the Loki proxy instead of forcing the UI to re-derive context client-side.** That keeps tooltip copy centralized, typed, and consistent between static fallback steps and real cross-stack `EDA_FLOW` events.

### 2026-05-27 — Dynamic sequence diagram from Loki EDA_FLOW

- **The ops sequence diagram can switch to true live mode by polling a small Next.js proxy route and treating `events.length > 0` as the feature toggle.** When Loki has no `EDA_FLOW` events yet, keep the static topology as the fallback. This allows operators to see the actual message flow topology in real time once backend instrumentation is rolled out.

- **Free-form `EDA_From` / `EDA_To` labels from Loki should still render against the fixed UC1 participant columns by mapping labels to `ParticipantId` values before sending them into the SVG renderer.** This keeps the diagram consistent even when the number of services involved in an issuance flow varies.

- **Next.js 15 dynamic API routes in this repo should keep `params` typed as a `Promise<...>` and await them inside the handler to satisfy the build-time type checks.** The Loki flow proxy route follows this pattern to work correctly with dynamic `[issuanceId]` segments.

- **Cross-stack observability enables live ops dashboards without backend changes to the diagram UI.** By querying Loki and falling back to the static topology, the frontend can render the actual message flow topology from both Java and .NET backends in a single unified sequence diagram with a live/static indicator. This enforces and validates the architecture's pub/subscribe semantics operationally.

### 2026-05-27 — Runtime backend switching + live labels

- **Shared frontend/backend helpers can stay in one module as long as server-only imports are `import type`.** Otherwise client pages pulling display helpers will break the client bundle. The backend selection logic is server-side in `/api/backend` and `/api/policies/[issuanceId]/flow`, keeping client code clean.

- **For runtime stack selection, a cookie-backed `/api/backend` route lets Next API proxies stay server-side while client components fetch the current label and flip the active backend without rebuilds.** The `BackendSwitcher` client island in the top nav uses this to display the active backend and allow operators to compare stacks without redeploying Next.js.

- **If a client page surfaces the active backend label, it should react to the switcher immediately (not just on reload).** A small `backend-changed` window event keeps the nav toggle and ops page copy in sync when the user changes backends at runtime.

### 2026-05-25 — UC3 File Processing Control Plane

- Next.js 15 catch-all API routes (`[...path]`) require `params` to be awaited as a `Promise<{ path: string[] }>` — do NOT destructure synchronously.
- SWR `refreshInterval` accepts a function `(data) => number` — use this to stop polling when the resource reaches a terminal state (Completed, PartialFailure, Failed, TimedOut).
- Inline TypeScript types per page (not shared type files) keeps each page self-contained and avoids import overhead — matches the existing codebase style.
- When proxying to upstream services, use `next: { revalidate: 0 }` on GET fetches to prevent Next.js from caching live status data.
- Progress bar color should reflect the batch outcome: blue=Processing, green=Completed, orange=PartialFailure, red=Failed.
### 2026-05-27 — Hover tooltips for sequence diagram arrows (frontend-2 complete)

- **Sequence diagram tooltip UX renders as HTML overlay, not SVG text.** The absolutely positioned HTML wrapper with Tailwind styling keeps hit areas clean, allows multi-line event metadata, and separates concerns between SVG rendering (shapes/arrows) and interaction (tooltips/hover). The tooltip displays event description, topic, direction, stack, and timestamp pulled from the flow endpoint's details object.

- **FlowEventDetails type centralized in Loki proxy response.** Rather than deriving descriptions on the frontend, the /api/policies/[issuanceId]/flow route enriches each FlowEvent with a typed details object including human-readable description, topic, direction, stack, and ISO timestamp. This keeps copy centralized and consistent with the backend observability contract.

- **Verification:** TypeScript clean, build passed, platform-ui container restarted. Live tooltips show EDA_FLOW event metadata on ops page hover. Static UC1_STEPS fallback displays when no live events available yet.
