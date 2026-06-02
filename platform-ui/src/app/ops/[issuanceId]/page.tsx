"use client";

import { useRef, useState } from "react";
import { useParams } from "next/navigation";
import useSWR from "swr";
import clsx from "clsx";
import Link from "next/link";
import type { LogEntry } from "@/app/api/loki/route";
import type { FlowEvent } from "@/types/eda-flow";

// ─── Participants ──────────────────────────────────────────────────────────

type Participant = {
  id: string;
  /** Short display name shown inside the lifeline box (use \n for line breaks). */
  label: string;
  /** Optional «qualifier» shown in small text above the label (e.g. endpoint name). */
  endpoint?: string;
  /** When true, renders a stick-figure actor icon instead of a rectangular box. */
  isActor?: boolean;
  color: string;
};

const DYNAMIC_COLORS = [
  "#f59e0b",
  "#8b5cf6",
  "#06b6d4",
  "#10b981",
  "#f43f5e",
  "#84cc16",
] as const;

// ─── Participant registry ─────────────────────────────────────────────────
//
// Convention (keep in sync with EDAFlowBehavior.cs HandlerToParticipant):
//   id         — matches EDA_From / EDA_To log values exactly (case-sensitive)
//   label      — display name; use \n to force a line-break in the SVG box
//   endpoint   — rendered as «endpoint» above the label; omit for actors/external
//   isActor    — true for the initiating human user
//
const PARTICIPANTS: readonly Participant[] = [
  // ── Actors ────────────────────────────────────────────────────────────
  { id: "user",                             label: "User",                          isActor: true,                          color: "#94a3b8" },

  // ── UC4 – dotnet-prs-appraisal handlers / sagas ───────────────────────
  { id: "AppraisalDocumentsController",     label: "Appraisal\nDocuments\nController",  endpoint: "prs-appraisal",          color: "#6366f1" },
  { id: "DocumentListSaga",                 label: "Document\nList Saga",               endpoint: "prs-appraisal",          color: "#0891b2" },
  { id: "DocumentRetrievalSaga",            label: "Document\nRetrieval Saga",          endpoint: "prs-appraisal",          color: "#67e8f9" },
  { id: "MainframeListAggregator",          label: "Mainframe\nList Aggregator",        endpoint: "prs-appraisal",          color: "#8b5cf6" },
  { id: "MainframeDocumentAggregator",      label: "Mainframe\nDoc Aggregator",         endpoint: "prs-appraisal",          color: "#a78bfa" },
  { id: "AtWorkDocumentListHandler",        label: "AtWork\nDoc List\nHandler",         endpoint: "prs-appraisal",          color: "#f59e0b" },
  { id: "AtWorkDocumentRetrievalHandler",   label: "AtWork\nDoc Retrieval\nHandler",    endpoint: "prs-appraisal",          color: "#fbbf24" },

  // ── External systems ───────────────────────────────────────────────────
  { id: "AtWork",                           label: "AtWork SQL",                        endpoint: "atwork",                 color: "#ea580c" },
  { id: "Mainframe",                        label: "IBM MQ\n(Mainframe)",               endpoint: "mainframe",              color: "#7c3aed" },
];

// ─── Saga status ordering ─────────────────────────────────────────────────

const STATUS_ORDER = [
  "Initiated",
  "AwaitingCompliance",
  "AwaitingAccountRecord",
  "AwaitingPAS",
  "PASConfirmed",
  "Completed",
] as const;

type SagaStatus = (typeof STATUS_ORDER)[number] | "Failed" | "ComplianceBlocked" | string;

function statusLevel(s: SagaStatus): number {
  const idx = (STATUS_ORDER as readonly string[]).indexOf(s);
  return idx; // -1 for Failed/blocked
}

// ─── UC1 message topology ─────────────────────────────────────────────────
// Each step declares which saga status level it becomes "done" at.
// "doneAtLevel" corresponds to STATUS_ORDER index (0=Initiated … 5=Completed).

type StepStatus = "done" | "active" | "pending";

interface Step {
  id: string;
  from: string;
  to: string;
  msg: string;
  isEvent: boolean;
  topic: string;
  doneAtLevel: number; // STATUS_ORDER index
  parallel?: boolean;
  note?: string;
  direction?: FlowEvent["direction"];
  handler?: string;
  timestamp?: string;
  payload?: Record<string, unknown>;
}

const UC1_STEPS: Step[] = [
  { id: "s01", from: "API",              to: "PolicyIssuance",    msg: "IssuePolicyCommand",                     isEvent: false, topic: "policy.commands.issue-policy",                             doneAtLevel: 0 },
  { id: "s02", from: "PolicyIssuance",   to: "Compliance",        msg: "PolicyIssuanceInitiatedEvent",           isEvent: true,  topic: "policy.events.policy-issuance-initiated",                   doneAtLevel: 1 },
  { id: "s03", from: "Compliance",       to: "PolicyIssuance",    msg: "ComplianceClearedEvent",                 isEvent: true,  topic: "compliance.events.compliance-cleared",                     doneAtLevel: 2 },
  { id: "s04", from: "PolicyIssuance",   to: "CustomerIdentity",  msg: "AccountLookupRequestedEvent",            isEvent: true,  topic: "customer.events.account-lookup-requested",                 doneAtLevel: 2 },
  { id: "s05", from: "CustomerIdentity", to: "PolicyIssuance",    msg: "AccountServiceRecordRetrievedEvent",     isEvent: true,  topic: "customer.events.account-service-record-retrieved",         doneAtLevel: 3 },
  { id: "s06", from: "PolicyIssuance",   to: "Integration",       msg: "IssuePolicyRequestedEvent",              isEvent: true,  topic: "policy.events.issue-policy-requested",                     doneAtLevel: 3 },
  { id: "s07", from: "Integration",      to: "PolicyIssuance",    msg: "PolicyAdminSystemResponseReceivedEvent", isEvent: true,  topic: "integration.events.policy-admin-system-response-received", doneAtLevel: 4, note: "fan-out ↓" },
  { id: "s08", from: "Billing",          to: "PolicyIssuance",    msg: "BillingAssociationCreatedEvent",         isEvent: true,  topic: "billing.events.billing-association-created",               doneAtLevel: 5 },
  { id: "s09", from: "CustomerIdentity", to: "PolicyIssuance",    msg: "CustomerUpdatedEvent",                   isEvent: true,  topic: "customer.events.customer-updated",                         doneAtLevel: 5, parallel: true, note: "∥ parallel" },
  { id: "s10", from: "PolicyIssuance",   to: "Notification",      msg: "PolicyIssuedEvent",                      isEvent: true,  topic: "policy.events.policy-issued",                              doneAtLevel: 5 },
];

function computeStepStatus(step: Step, sagaStatus: SagaStatus, isLive = false): StepStatus {
  if (isLive) return "done";
  if (sagaStatus === "Completed") return "done";
  const current = statusLevel(sagaStatus);
  if (current < 0) return "pending"; // Failed / ComplianceBlocked
  if (current > step.doneAtLevel) return "done";
  if (current === step.doneAtLevel) return "active";
  return "pending";
}

function formatParticipantLabel(id: string) {
  return id
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim();
}

function getDynamicColor(id: string) {
  const hash = [...id].reduce((total, char) => total + char.charCodeAt(0), 0);
  return DYNAMIC_COLORS[hash % DYNAMIC_COLORS.length];
}

function createDynamicParticipant(id: string, index: number): Participant {
  return {
    id,
    label: formatParticipantLabel(id),
    color: DYNAMIC_COLORS[index % DYNAMIC_COLORS.length],
  };
}

const SUPPRESSED_IDS = new Set(["broadcast", "allsubscribers"]);

function isSuppressedId(id: string) {
  return SUPPRESSED_IDS.has(id.toLowerCase());
}

function buildVisibleParticipants(events: FlowEvent[], isLiveMode: boolean): Participant[] {
  if (!(isLiveMode && events.length > 0)) {
    // No live events yet — return an empty list; the caller renders a
    // "Waiting for trace data…" placeholder instead of a stale diagram.
    return [];
  }

  // Collect ordered unique participant IDs from live events, filtering artefacts.
  const seen = new Set<string>();
  const orderedIds: string[] = [];

  for (const event of events) {
    for (const id of [event.from, event.to]) {
      if (!seen.has(id) && !isSuppressedId(id)) {
        seen.add(id);
        orderedIds.push(id);
      }
    }
  }

  // Prepend the "user" actor when an AppraisalDocumentsController is present
  // (UC4 flow entry point) and not already in the list.
  const hasController = orderedIds.includes("AppraisalDocumentsController");
  if (hasController && !seen.has("user")) {
    orderedIds.unshift("user");
  }

  let dynamicIndex = 0;
  return orderedIds.map((id) => {
    const known = PARTICIPANTS.find((p) => p.id === id);
    if (known) return known;
    const dynamic = createDynamicParticipant(id, dynamicIndex);
    dynamicIndex += 1;
    return dynamic;
  });
}

function getParticipantColor(id: string, participants: readonly Participant[]) {
  return participants.find((participant) => participant.id === id)?.color ?? getDynamicColor(id);
}

function flowEventsToSteps(events: FlowEvent[]): Step[] {
  const filtered = events.filter(
    (event) =>
      !isSuppressedId(event.from) &&
      !isSuppressedId(event.to) &&
      // Skip HTTP-type synthetic entries — they're re-added as the first step below
      // so that the isEvent/dashed-vs-solid style is always correct.
      !event.messageType.startsWith("HTTP ")
  );

  const steps: Step[] = filtered.map((event, index) => ({
    id: `live-${index}`,
    from: event.from,
    to: event.to,
    msg: event.messageType,
    isEvent: !event.messageType.endsWith("Command"),
    topic: event.topic,
    doneAtLevel: 5,
    direction: event.direction,
    handler: event.handler,
    timestamp: event.timestamp,
    payload: event.payload,
  }));

  // Prepend a synthetic HTTP entry-point step for UC4 flows so the User actor
  // is always the first lifeline and the request origin is visible.
  const hasController = filtered.some(
    (e) => e.from === "AppraisalDocumentsController" || e.to === "AppraisalDocumentsController"
  );
  if (hasController) {
    const firstTs = filtered[0]?.timestamp;
    steps.unshift({
      id: "live-http-entry",
      from: "user",
      to: "AppraisalDocumentsController",
      msg: "HTTP GET /api/documents",
      isEvent: false,
      topic: "http",
      doneAtLevel: 5,
      direction: "consumed",
      timestamp: firstTs,
    });
  }

  return steps;
}

function getFanoutGroups(steps: Step[]) {
  const groups: Array<{ start: number; end: number; count: number }> = [];

  for (let index = 0; index < steps.length; index += 1) {
    const current = steps[index];
    let next = index + 1;

    while (
      next < steps.length &&
      steps[next].msg === current.msg &&
      steps[next].from === current.from
    ) {
      next += 1;
    }

    const group = steps.slice(index, next);
    const uniqueTargets = new Set(group.map((step) => step.to));
    if (group.length > 1 && uniqueTargets.size > 1) {
      groups.push({ start: index, end: next - 1, count: uniqueTargets.size });
    }

    index = next - 1;
  }

  return groups;
}

interface SequenceDiagramProps {
  sagaStatus: SagaStatus;
  liveSteps?: FlowEvent[];
  isLiveMode?: boolean;
}

// ─── SVG layout constants ─────────────────────────────────────────────────

const LEFT_MARGIN   = 76;
const COL_WIDTH     = 168;
const BOX_WIDTH     = 138;
const BOX_HEIGHT    = 64;   // tall enough for endpoint + 2-line label
const ACTOR_HEIGHT  = 58;   // height of actor figure + gap to lifeline
const HEADER_HEIGHT = 90;   // total header zone
const ROW_HEIGHT    = 50;
const BOTTOM_PAD    = 36;

function colX(i: number) {
  return LEFT_MARGIN + i * COL_WIDTH + COL_WIDTH / 2;
}

function formatTime(iso: string) {
  try {
    return new Date(iso).toTimeString().slice(0, 8);
  } catch {
    return "";
  }
}

// ─── SVG text helpers ────────────────────────────────────────────────────

/** Split a label on \n, centre each line inside the box, and return <tspan> elements. */
function LabelLines({
  lines,
  cx,
  baseY,
  fontSize,
  fontWeight,
  fill,
  lineHeight = 13,
}: {
  lines: string[];
  cx: number;
  baseY: number;
  fontSize: number;
  fontWeight: string | number;
  fill: string;
  lineHeight?: number;
}) {
  const totalHeight = (lines.length - 1) * lineHeight;
  const startY = baseY - totalHeight / 2;
  return (
    <>
      {lines.map((line, i) => (
        <text
          key={i}
          x={cx}
          y={startY + i * lineHeight}
          textAnchor="middle"
          fontSize={fontSize}
          fontWeight={fontWeight}
          fill={fill}
        >
          {line}
        </text>
      ))}
    </>
  );
}

/** Stick-figure actor icon centred at cx with the bottom at bottomY. */
function ActorIcon({ cx, bottomY, color }: { cx: number; bottomY: number; color: string }) {
  const headR = 7;
  const bodyH = 16;
  const armSpan = 12;
  const legSpread = 10;
  const headCy = bottomY - bodyH - headR * 2;
  const shoulderY = headCy + headR * 2;
  const hipY = shoulderY + bodyH;
  return (
    <g stroke={color} strokeWidth={1.6} fill="none">
      <circle cx={cx} cy={headCy} r={headR} fill={color + "33"} />
      <line x1={cx} y1={shoulderY} x2={cx} y2={hipY} />
      <line x1={cx - armSpan} y1={shoulderY + 4} x2={cx + armSpan} y2={shoulderY + 4} />
      <line x1={cx} y1={hipY} x2={cx - legSpread} y2={hipY + bodyH * 0.7} />
      <line x1={cx} y1={hipY} x2={cx + legSpread} y2={hipY + bodyH * 0.7} />
    </g>
  );
}

// ─── Sequence Diagram ────────────────────────────────────────────────────

function SequenceDiagram({ sagaStatus, liveSteps = [], isLiveMode = false }: SequenceDiagramProps) {
  const stepsToRender = isLiveMode && liveSteps.length > 0 ? flowEventsToSteps(liveSteps) : [];
  const fanoutGroups = isLiveMode ? getFanoutGroups(stepsToRender) : [];
  const visibleParticipants = buildVisibleParticipants(liveSteps, isLiveMode);
  const participantIndex = new Map<string, number>(visibleParticipants.map((participant, index) => [participant.id, index]));
  // Hooks must be called unconditionally before any early return.
  const svgRef = useRef<SVGSVGElement>(null);
  const [tooltip, setTooltip] = useState<{
    step: Step | FlowEvent | null;
    x: number;
    y: number;
  } | null>(null);

  // No live events yet — render a placeholder rather than an empty/stale diagram.
  if (visibleParticipants.length === 0) {
    return (
      <div className="py-12 text-center space-y-2">
        <p className="text-2xl">📡</p>
        <p className="text-sm animate-pulse" style={{ color: "var(--muted)" }}>
          ⏳ Waiting for trace data…
        </p>
        <p className="text-xs" style={{ color: "var(--muted)" }}>
          Trigger a flow to see the live sequence diagram.
        </p>
      </div>
    );
  }

  const svgWidth = LEFT_MARGIN + visibleParticipants.length * COL_WIDTH + 20;
  const svgHeight = HEADER_HEIGHT + stepsToRender.length * ROW_HEIGHT + BOTTOM_PAD;
  const lifelineBottom = svgHeight - BOTTOM_PAD;

  return (
    <div>
      <div className="mb-2 flex items-center gap-2">
        <span
          className="rounded-full px-2 py-0.5 text-xs font-semibold"
          style={{
            background: isLiveMode ? "#16a34a22" : "#6366f122",
            color: isLiveMode ? "#4ade80" : "#818cf8",
            border: `1px solid ${isLiveMode ? "#16a34a" : "#6366f1"}`,
          }}
        >
          {isLiveMode ? "📡 Live — from Loki" : "⏳ Awaiting trace data"}
        </span>
      </div>

      <div style={{ position: "relative", width: svgWidth }}>
        <svg
          ref={svgRef}
          width={svgWidth}
          height={svgHeight}
          viewBox={`0 0 ${svgWidth} ${svgHeight}`}
          style={{ fontFamily: "ui-sans-serif, system-ui, sans-serif", display: "block" }}
        >
          <defs>
            {visibleParticipants.map((participant) => (
              <marker
                key={`arrow-${participant.id}`}
                id={`arrow-${participant.id}`}
                markerWidth="9"
                markerHeight="9"
                refX="8"
                refY="3"
                orient="auto"
              >
                <path d="M0,0 L0,6 L8,3 z" fill={participant.color} />
              </marker>
            ))}
            <marker id="arrow-pending" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#4b5563" />
            </marker>
          </defs>

          {stepsToRender.map((_, index) =>
            index % 2 === 0 ? (
              <rect
                key={`stripe-${index}`}
                x={0}
                y={HEADER_HEIGHT + index * ROW_HEIGHT}
                width={svgWidth}
                height={ROW_HEIGHT}
                fill="rgba(255,255,255,0.018)"
              />
            ) : null
          )}

          {fanoutGroups.map((group) => {
            const topY = HEADER_HEIGHT + group.start * ROW_HEIGHT + 8;
            const bottomY = HEADER_HEIGHT + (group.end + 1) * ROW_HEIGHT - 8;
            const midY = (topY + bottomY) / 2;
            const bracketX = LEFT_MARGIN - 48;

            return (
              <g key={`fanout-${group.start}`}>
                <path
                  d={`M ${bracketX + 12} ${topY} H ${bracketX} V ${bottomY} H ${bracketX + 12}`}
                  fill="none"
                  stroke="#94a3b8"
                  strokeWidth={1.4}
                  opacity={0.65}
                />
                <text
                  x={bracketX - 4}
                  y={midY - 4}
                  textAnchor="end"
                  fontSize={9}
                  fontWeight="600"
                  fill="#94a3b8"
                >
                  fan-out
                </text>
                <text x={bracketX - 4} y={midY + 8} textAnchor="end" fontSize={8} fill="#64748b">
                  ↓ {group.count} subscribers
                </text>
              </g>
            );
          })}

          {visibleParticipants.map((participant, index) => {
            const cx = colX(index);
            const labelLines = participant.label.split("\n");

            if (participant.isActor) {
              // Render a stick-figure actor with name below
              return (
                <g key={`hdr-${participant.id}`}>
                  <ActorIcon cx={cx} bottomY={HEADER_HEIGHT - 8} color={participant.color} />
                  <text
                    x={cx}
                    y={HEADER_HEIGHT - 2}
                    textAnchor="middle"
                    fill={participant.color}
                    fontSize={10}
                    fontWeight="600"
                  >
                    {participant.label}
                  </text>
                </g>
              );
            }

            // Normal rectangular lifeline box with optional «endpoint» qualifier
            const boxTop  = 6;
            const boxMidY = boxTop + BOX_HEIGHT / 2;
            // Vertical layout: qualifier (if any) + label lines
            const hasQualifier = Boolean(participant.endpoint);
            const qualifierY   = boxTop + 12;
            const labelBaseY   = hasQualifier
              ? qualifierY + 10 + (labelLines.length * 13) / 2 - 2
              : boxMidY + (labelLines.length <= 1 ? 4 : 0);

            return (
              <g key={`hdr-${participant.id}`}>
                <rect
                  x={cx - BOX_WIDTH / 2}
                  y={boxTop}
                  width={BOX_WIDTH}
                  height={BOX_HEIGHT}
                  rx={6}
                  fill={participant.color}
                />
                {hasQualifier && (
                  <text
                    x={cx}
                    y={qualifierY}
                    textAnchor="middle"
                    fill="rgba(255,255,255,0.60)"
                    fontSize={8}
                    fontStyle="italic"
                  >
                    &#171;{participant.endpoint}&#187;
                  </text>
                )}
                <LabelLines
                  lines={labelLines}
                  cx={cx}
                  baseY={labelBaseY}
                  fontSize={10}
                  fontWeight="700"
                  fill="white"
                  lineHeight={13}
                />
              </g>
            );
          })}

          {visibleParticipants.map((participant, index) => (
            <line
              key={`ll-${participant.id}`}
              x1={colX(index)}
              y1={HEADER_HEIGHT}
              x2={colX(index)}
              y2={lifelineBottom}
              stroke="#2d3148"
              strokeWidth={1}
              strokeDasharray="4 4"
            />
          ))}

          {stepsToRender.map((step, index) => {
            const rowY = HEADER_HEIGHT + index * ROW_HEIGHT + ROW_HEIGHT / 2;
            const fromIdx = participantIndex.get(step.from);
            const toIdx = participantIndex.get(step.to);

            if (fromIdx === undefined || toIdx === undefined) {
              return null;
            }

            const fromX = colX(fromIdx);
            const toX = colX(toIdx);
            const status = computeStepStatus(step, sagaStatus, isLiveMode);
            const participantColor = getParticipantColor(step.from, visibleParticipants);
            const color = status === "pending" ? "#374151" : participantColor;
            const opacity = status === "pending" ? 0.42 : 1;
            const direction = toX > fromX ? 1 : -1;
            const lineEnd = toX - direction;
            const markerId = status === "pending" ? "arrow-pending" : `arrow-${step.from}`;
            const dash = step.isEvent ? "6 3" : undefined;
            const dotFill = status === "done" ? "#22c55e22" : status === "active" ? "#f59e0b22" : "#1f2937";
            const dotStroke = status === "done" ? "#22c55e" : status === "active" ? "#f59e0b" : "#374151";
            const hoverStep = isLiveMode ? liveSteps[index] ?? step : step;

            return (
              <g
                key={step.id}
                opacity={opacity}
                onMouseEnter={(event) => {
                  const svgRect = svgRef.current?.getBoundingClientRect();
                  setTooltip({
                    step: hoverStep,
                    x: event.clientX - (svgRect?.left ?? 0),
                    y: event.clientY - (svgRect?.top ?? 0),
                  });
                }}
                onMouseMove={(event) => {
                  const svgRect = svgRef.current?.getBoundingClientRect();
                  setTooltip({
                    step: hoverStep,
                    x: event.clientX - (svgRect?.left ?? 0),
                    y: event.clientY - (svgRect?.top ?? 0),
                  });
                }}
                onMouseLeave={() => setTooltip(null)}
                style={{ cursor: "pointer" }}
              >
                <circle cx={LEFT_MARGIN - 24} cy={rowY} r={6} fill={dotFill} stroke={dotStroke} strokeWidth={1.5} />
                {status === "done" && (
                  <text x={LEFT_MARGIN - 24} y={rowY + 4} textAnchor="middle" fontSize={8} fill="#22c55e">✓</text>
                )}

                <text x={LEFT_MARGIN - 10} y={rowY + 4} textAnchor="end" fontSize={10} fill="#6b7280">{index + 1}</text>

                <line x1={fromX} y1={rowY} x2={lineEnd} y2={rowY} stroke="transparent" strokeWidth={14} />
                <line
                  x1={fromX}
                  y1={rowY}
                  x2={lineEnd}
                  y2={rowY}
                  stroke={color}
                  strokeWidth={status === "active" ? 2.5 : 1.8}
                  strokeDasharray={dash}
                  markerEnd={`url(#${markerId})`}
                />

                <text
                  x={(fromX + toX) / 2}
                  y={rowY - 8}
                  textAnchor="middle"
                  fontSize={10}
                  fontWeight={step.isEvent ? "400" : "600"}
                  fontStyle={step.isEvent ? "italic" : "normal"}
                  fill={color}
                >
                  {step.msg}
                </text>
              </g>
            );
          })}
        </svg>

        {tooltip?.step && (
          <div
            style={{
              position: "absolute",
              left: Math.min(tooltip.x + 12, svgWidth - 300),
              top: Math.max(tooltip.y - 8, 8),
              zIndex: 50,
              pointerEvents: "none",
            }}
            className="w-72 rounded-lg border border-gray-600 bg-gray-900 p-3 text-xs shadow-xl"
          >
            {/* Event / message name */}
            <div className="mb-2 font-bold text-white text-[13px]">
              {"messageType" in tooltip.step ? tooltip.step.messageType : tooltip.step.msg}
            </div>

            {/* Actual message payload properties */}
            {(() => {
              const step = tooltip.step;
              const payload = "payload" in step ? step.payload : undefined;
              if (!payload || Object.keys(payload).length === 0) return null;
              return (
                <div className="mb-2">
                  <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-gray-500">Properties</p>
                  <div className="space-y-0.5">
                    {Object.entries(payload).map(([k, v]) => (
                      <div key={k} className="flex gap-2 text-[10px]">
                        <span className="font-mono text-gray-400 shrink-0">{k}:</span>
                        <span className="font-mono text-cyan-300 break-all">{String(v ?? "")}</span>
                      </div>
                    ))}
                  </div>
                </div>
              );
            })()}

            {/* Time only */}
            <div className="border-t border-gray-700 pt-2 font-mono text-[10px] text-gray-400">
              {"details" in tooltip.step && tooltip.step.details ? (
                <div>🕐 {new Date(tooltip.step.details.timestamp).toLocaleTimeString()}</div>
              ) : (
                "timestamp" in tooltip.step && tooltip.step.timestamp ? (
                  <div>🕐 {formatTime(tooltip.step.timestamp)}</div>
                ) : null
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Saga State types ────────────────────────────────────────────────────

type SagaRecord = {
  issuanceId: string;
  accountId: string;
  status: string;
  targetPas: string | null;
  policyNumbers: string[] | null;
  policyTypeCode: number;
  submittingChannel: string;
  accountServiceRequestNumber: string | null;
  billingComplete: boolean;
  customerUpdateComplete: boolean;
  failureReason: string | null;
  requestedAt: string;
  completedAt: string | null;
};

const STATUS_COLOR: Record<string, string> = {
  Initiated:              "#6366f1",
  AwaitingCompliance:     "#f59e0b",
  AwaitingAccountRecord:  "#f59e0b",
  AwaitingPAS:            "#f59e0b",
  PASConfirmed:           "#06b6d4",
  Completed:              "#22c55e",
  Failed:                 "#ef4444",
  ComplianceBlocked:      "#ef4444",
};

const LEVEL_STYLE: Record<string, { color: string; label: string }> = {
  ERROR: { color: "#ef4444", label: "ERR" },
  WARN:  { color: "#f59e0b", label: "WRN" },
  INFO:  { color: "#94a3b8", label: "INF" },
  DEBUG: { color: "#6b7280", label: "DBG" },
};

type Uc4PanelStatus = "done" | "active" | "pending";

type Uc4TimelineChild = {
  label: string;
  status: Uc4PanelStatus;
  timestamp?: string;
};

type Uc4TimelineItem = {
  id: string;
  label: string;
  status: Uc4PanelStatus;
  timestamp?: string;
  detail?: string;
  children?: Uc4TimelineChild[];
};

const UC4_FLOW_SIGNATURES = [
  "GetAppraisalDocumentList",
  "RetrieveAppraisalDocument",
  "Uc4Appraisal",
  "DocumentListSaga",
  "DocumentRetrievalSaga",
  "MainframeListAggregator",
  "MainframeDocumentAggregator",
] as const;

const UC4_LIST_REQUEST_SIGNATURES = [
  "GetAppraisalDocumentListCommand",
  "GetAppraisalDocumentListRequest",
  "AppraisalDocumentListRequested",
  "Uc4AppraisalDocumentListRequestedEvent",
] as const;

const UC4_SCATTER_SIGNATURES = [
  "AppraisalDocumentListRequested",
  "Uc4AppraisalDocumentListRequestedEvent",
] as const;

const UC4_ATWORK_QUERY_SIGNATURES = ["AtWorkDocumentListHandler", "AtWorkHandler", "AtWorkListQuery"] as const;
const UC4_MAINFRAME_QUERY_SIGNATURES = ["MainframeListAggregator", "MqListRequest"] as const;
const UC4_ATWORK_COMPLETE_SIGNATURES = ["AtWorkDocumentListCompletedEvent", "AtWorkDocumentListCompleted"] as const;
const UC4_MAINFRAME_PART_SIGNATURES = ["MainframeAppraisalListPartReceivedEvent", "MqListReply"] as const;
const UC4_MAINFRAME_COMPLETE_SIGNATURES = ["MainframeDocumentListCompletedEvent", "MainframeListComplete"] as const;
// "List Complete" is considered done once both scatter-gather branches have completed.
// GetAppraisalDocumentListResponse is no longer sent (MongoDB polling replaced Callbacks).
const UC4_LIST_COMPLETE_SIGNATURES = [
  "AtWorkDocumentListCompletedEvent",
  "MainframeDocumentListCompletedEvent",
] as const;
const UC4_RETRIEVAL_REQUEST_SIGNATURES = ["RetrieveAppraisalDocumentCommand", "RetrieveAppraisalDocumentRequest"] as const;
// RetrieveAppraisalDocumentResponse is no longer sent (MongoDB polling replaced Callbacks).
const UC4_RETRIEVAL_COMPLETE_SIGNATURES = [
  "AtWorkDocumentRetrievedEvent",
  "AppraisalDocumentRetrievedEvent",
  "RetrieveAppraisalDocumentResponse",
] as const;

function includesIgnoreCase(value: string, needle: string) {
  return value.toLowerCase().includes(needle.toLowerCase());
}

function eventMatches(event: FlowEvent, signatures: readonly string[]) {
  const haystack = [event.messageType, event.from, event.to, event.handler]
    .filter(Boolean)
    .join(" ");

  return signatures.some((signature) => includesIgnoreCase(haystack, signature));
}

function findFirstEvent(events: FlowEvent[], signatures: readonly string[]) {
  return events.find((event) => eventMatches(event, signatures));
}

function findLastEvent(events: FlowEvent[], signatures: readonly string[]) {
  for (let index = events.length - 1; index >= 0; index -= 1) {
    if (eventMatches(events[index], signatures)) {
      return events[index];
    }
  }

  return undefined;
}

function countEvents(events: FlowEvent[], signatures: readonly string[]) {
  return events.filter((event) => eventMatches(event, signatures)).length;
}

function getUc4StatusColor(status: Uc4PanelStatus) {
  if (status === "done") return "#22c55e";
  if (status === "active") return "#f59e0b";
  return "#475569";
}

function isUc4Flow(events: FlowEvent[]): boolean {
  return events.some((event) => eventMatches(event, UC4_FLOW_SIGNATURES));
}

function deriveUc4SagaView(events: FlowEvent[]) {
  const orderedEvents = [...events].sort((left, right) => left.timestamp.localeCompare(right.timestamp));
  const firstEvent = orderedEvents[0];
  const lastEvent = orderedEvents.at(-1);
  const listRequested = findFirstEvent(orderedEvents, UC4_LIST_REQUEST_SIGNATURES);
  const scatterStarted = findFirstEvent(orderedEvents, UC4_SCATTER_SIGNATURES);
  const atWorkQuery = findFirstEvent(orderedEvents, UC4_ATWORK_QUERY_SIGNATURES);
  const mainframeQuery = findFirstEvent(orderedEvents, UC4_MAINFRAME_QUERY_SIGNATURES);
  const atWorkComplete = findFirstEvent(orderedEvents, UC4_ATWORK_COMPLETE_SIGNATURES);
  const mainframeComplete = findFirstEvent(orderedEvents, UC4_MAINFRAME_COMPLETE_SIGNATURES);

  const branchesComplete = Boolean(atWorkComplete && mainframeComplete);
  // "List Complete" is considered done once both scatter-gather branches have reported back.
  // GetAppraisalDocumentListResponse is no longer sent (MongoDB polling replaced Callbacks).
  const listComplete = branchesComplete
    ? findLastEvent(orderedEvents, UC4_LIST_COMPLETE_SIGNATURES)
    : undefined;
  const retrievalRequests = countEvents(orderedEvents, UC4_RETRIEVAL_REQUEST_SIGNATURES);
  const retrievalCompleteCount = countEvents(orderedEvents, UC4_RETRIEVAL_COMPLETE_SIGNATURES);
  const mainframePartCount = countEvents(orderedEvents, UC4_MAINFRAME_PART_SIGNATURES);
  const activeHandlers = [...new Set(orderedEvents.map((event) => event.handler).filter(Boolean))];
  const subscribers = [
    ...new Set(
      orderedEvents
        .filter((event) => event.direction === "handled")
        .map((event) => event.handler ?? event.to)
        .filter(Boolean)
    ),
  ];

  const retrievalStatus: Uc4PanelStatus = retrievalRequests === 0
    ? "pending"
    : retrievalCompleteCount >= retrievalRequests
      ? "done"
      : "active";

  const timeline: Uc4TimelineItem[] = [
    {
      id: "requested",
      label: "Document List Requested",
      status: listRequested ? "done" : "pending",
      timestamp: listRequested?.timestamp,
    },
    {
      id: "scatter",
      label: "Scatter-Gather Fanout",
      status: scatterStarted ? "done" : listRequested ? "active" : "pending",
      timestamp: scatterStarted?.timestamp,
      children: [
        {
          label: "AtWork query sent",
          status: atWorkQuery ? "done" : scatterStarted ? "active" : "pending",
          timestamp: atWorkQuery?.timestamp,
        },
        {
          label: mainframePartCount > 0 ? `Mainframe query sent (${mainframePartCount} parts)` : "Mainframe query sent",
          status: mainframeQuery ? "done" : scatterStarted ? "active" : "pending",
          timestamp: mainframeQuery?.timestamp,
        },
      ],
    },
    {
      id: "branches",
      label: "Branches Complete",
      status: branchesComplete ? "done" : atWorkComplete || mainframeComplete ? "active" : scatterStarted ? "active" : "pending",
      timestamp: (mainframeComplete ?? atWorkComplete)?.timestamp,
      detail: `AtWork ${atWorkComplete ? "complete" : "pending"} · Mainframe ${mainframeComplete ? "complete" : mainframePartCount > 0 ? `${mainframePartCount} parts received` : "pending"}`,
    },
    {
      id: "list-complete",
      label: "List Complete",
      status: listComplete ? "done" : branchesComplete ? "active" : "pending",
      timestamp: listComplete?.timestamp,
    },
    {
      id: "retrieval",
      label: retrievalRequests > 0 ? `Documents Retrieved (${retrievalCompleteCount}/${retrievalRequests})` : "Documents Retrieved",
      status: retrievalStatus,
      timestamp: findLastEvent(orderedEvents, UC4_RETRIEVAL_COMPLETE_SIGNATURES)?.timestamp,
      detail: retrievalRequests > 0 ? `${retrievalCompleteCount} completed responses observed` : "No retrieval requests observed yet",
    },
  ];

  const currentStage = [...timeline].reverse().find((item) => item.status !== "pending")?.label ?? "Waiting for UC4 saga activity";

  const subSagas = [
    {
      label: "DocumentListSaga",
      status: listComplete ? "Complete" : listRequested ? "Active" : "Pending",
      detail: listComplete ? `Completed at ${formatTime(listComplete.timestamp)}` : listRequested ? "Tracking branch completion" : "Awaiting command",
    },
    {
      label: "MainframeListAggregator",
      status: mainframeComplete ? "Complete" : mainframeQuery || mainframePartCount > 0 ? "Active" : "Pending",
      detail: mainframeComplete ? "All expected list parts merged" : `${mainframePartCount} part(s) received`,
    },
    {
      label: "DocumentRetrievalSagas",
      status: retrievalRequests === 0 ? "Idle" : retrievalCompleteCount >= retrievalRequests ? "Complete" : "Active",
      detail: retrievalRequests === 0 ? "No document retrievals yet" : `${retrievalCompleteCount}/${retrievalRequests} response(s) returned`,
    },
  ];

  const keyFields = [
    { label: "Request ID", value: firstEvent?.issuanceId ?? "—" },
    { label: "Current Stage", value: currentStage },
    { label: "Subscribers", value: subscribers.length > 0 ? subscribers.join(", ") : "—" },
    { label: "Handlers", value: activeHandlers.length > 0 ? activeHandlers.join(", ") : "—" },
    {
      label: "Observed Window",
      value: firstEvent && lastEvent ? `${formatTime(firstEvent.timestamp)} → ${formatTime(lastEvent.timestamp)}` : "—",
    },
    { label: "Latest Message", value: lastEvent?.messageType ?? "—" },
  ];

  return { timeline, currentStage, subSagas, keyFields };
}

function Uc1SagaPanel({ saga, sagaStatus }: { saga: SagaRecord; sagaStatus: SagaStatus }) {
  return (
    <div className="space-y-3 rounded-lg border p-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
      <p className="text-sm font-semibold">Saga Details</p>
      {[
        { label: "Account", value: saga.accountId },
        { label: "Channel", value: saga.submittingChannel },
        { label: "PolicyType", value: String(saga.policyTypeCode) },
        { label: "PAS", value: saga.targetPas ?? "—" },
        { label: "Policy #", value: saga.policyNumbers?.join(", ") ?? "—" },
        { label: "Started", value: new Date(saga.requestedAt).toLocaleTimeString() },
        { label: "Ended", value: saga.completedAt ? new Date(saga.completedAt).toLocaleTimeString() : "—" },
      ].map(({ label, value }) => (
        <div key={label} className="flex justify-between gap-2 text-xs">
          <span style={{ color: "var(--muted)" }}>{label}</span>
          <span className="max-w-[180px] truncate text-right font-mono" style={{ color: "var(--text)" }}>
            {value}
          </span>
        </div>
      ))}

      {saga.failureReason && (
        <div className="rounded px-2 py-1.5 text-xs" style={{ background: "#2d1515", color: "var(--danger)" }}>
          {saga.failureReason}
        </div>
      )}

      {statusLevel(sagaStatus) >= 4 && (
        <div className="space-y-2 border-t pt-1" style={{ borderColor: "var(--border)" }}>
          <p className="text-xs font-semibold" style={{ color: "var(--muted)" }}>Parallel Branches</p>
          {[
            { label: "Billing", done: saga.billingComplete },
            { label: "Customer Update", done: saga.customerUpdateComplete },
          ].map(({ label, done }) => (
            <div key={label} className="flex items-center justify-between text-xs">
              <span style={{ color: "var(--muted)" }}>{label}</span>
              <span style={{ color: done ? "#22c55e" : "#f59e0b" }}>
                {done ? "✓ complete" : "⏳ waiting"}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function Uc4SagaPanel({ events, issuanceId }: { events: FlowEvent[]; issuanceId: string }) {
  const { timeline, currentStage, subSagas, keyFields } = deriveUc4SagaView(events);
  const [mongoModal, setMongoModal] = useState<{ title: string; data: unknown } | null>(null);
  const { data: liveListSaga } = useSWR<{
    documentListSaga?: Record<string, unknown> & { documentCount?: number; status?: string };
  }>(
    issuanceId ? `/api/appraisals/flow-sagas?requestId=${encodeURIComponent(issuanceId)}` : null,
    fetcher,
    { refreshInterval: 3000 }
  );

  return (
    <>
      <div className="space-y-4 rounded-lg border p-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-sm font-semibold">Saga Details</p>
            <p className="mt-1 text-xs" style={{ color: "var(--muted)" }}>State derived from live UC4 flow events.</p>
          </div>
          <span
            className="rounded-full px-2 py-0.5 text-[10px] font-semibold"
            style={{ background: "#06b6d422", border: "1px solid #0891b2", color: "#67e8f9" }}
          >
            {currentStage}
          </span>
        </div>

        <div className="space-y-3">
          {timeline.map((item, index) => {
            const color = getUc4StatusColor(item.status);
            const isLast = index === timeline.length - 1;
            return (
              <div key={item.id} className="flex gap-3">
                <div className="flex w-4 flex-col items-center">
                  <span className="mt-0.5 h-3 w-3 rounded-full border-2" style={{ background: `${color}22`, borderColor: color }} />
                  {!isLast && <span className="mt-1 h-full w-px" style={{ background: "var(--border)" }} />}
                </div>
                <div className="min-w-0 flex-1 pb-3">
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-xs font-semibold" style={{ color }}>{item.label}</span>
                    <span className="text-[10px] font-mono" style={{ color: "var(--muted)" }}>
                      {item.timestamp ? formatTime(item.timestamp) : item.status === "pending" ? "pending" : "active"}
                    </span>
                  </div>
                  {item.detail && (
                    <p className="mt-1 text-[11px]" style={{ color: "var(--muted)" }}>{item.detail}</p>
                  )}
                  {item.children && (
                    <div className="mt-2 space-y-1 border-l pl-3" style={{ borderColor: "var(--border)" }}>
                      {item.children.map((child) => (
                        <div key={child.label} className="flex items-center justify-between gap-2 text-[11px]">
                          <span style={{ color: child.status === "pending" ? "var(--muted)" : getUc4StatusColor(child.status) }}>
                            {child.label}
                          </span>
                          <span className="font-mono" style={{ color: "var(--muted)" }}>
                            {child.timestamp ? formatTime(child.timestamp) : child.status === "pending" ? "—" : "…"}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {liveListSaga && (
          <div className="border-t pt-3" style={{ borderColor: "var(--border)" }}>
            <div className="flex items-center justify-between">
              <p className="text-xs font-semibold" style={{ color: "var(--muted)" }}>Live Saga Data</p>
              <button
                className="rounded px-2 py-0.5 text-[10px] font-semibold"
                style={{ background: "#0891b222", border: "1px solid #0891b2", color: "#67e8f9" }}
                onClick={() => setMongoModal({ title: "Document List Saga", data: liveListSaga.documentListSaga })}
              >
                View
              </button>
            </div>
            {liveListSaga.documentListSaga && (
              <p className="mt-1 text-[11px]" style={{ color: "var(--muted)" }}>
                {String(liveListSaga.documentListSaga.documentCount ?? 0)} document(s) · {String(liveListSaga.documentListSaga.status ?? "Unknown")}
              </p>
            )}
          </div>
        )}

        <div className="space-y-2 border-t pt-3" style={{ borderColor: "var(--border)" }}>
          <p className="text-xs font-semibold" style={{ color: "var(--muted)" }}>Sub-sagas</p>
          {subSagas.map((item) => (
            <div key={item.label} className="rounded border px-3 py-2 text-xs" style={{ borderColor: "var(--border)", background: "rgba(15,23,42,0.25)" }}>
              <div className="flex items-center justify-between gap-2">
                <span style={{ color: "var(--text)" }}>{item.label}</span>
                <span
                  className="rounded-full px-2 py-0.5 text-[10px] font-semibold"
                  style={{
                    background: `${item.status === "Complete" ? "#22c55e" : item.status === "Active" ? "#f59e0b" : "#64748b"}22`,
                    color: item.status === "Complete" ? "#4ade80" : item.status === "Active" ? "#fbbf24" : "#cbd5e1",
                  }}
                >
                  {item.status}
                </span>
              </div>
              <p className="mt-1 text-[11px]" style={{ color: "var(--muted)" }}>{item.detail}</p>
            </div>
          ))}
        </div>

        <div className="space-y-2 border-t pt-3" style={{ borderColor: "var(--border)" }}>
          <p className="text-xs font-semibold" style={{ color: "var(--muted)" }}>Key Saga Data</p>
          {keyFields.map(({ label, value }) => (
            <div key={label} className="flex justify-between gap-3 text-xs">
              <span style={{ color: "var(--muted)" }}>{label}</span>
              <span className="max-w-[180px] text-right font-mono text-[11px]" style={{ color: "var(--text)" }}>
                {value}
              </span>
            </div>
          ))}
        </div>
      </div>

      {mongoModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70"
          onClick={() => setMongoModal(null)}
        >
          <div
            className="max-h-[80vh] w-[600px] overflow-auto rounded-lg border border-gray-600 bg-gray-900 p-4 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-center justify-between">
              <p className="font-semibold text-white text-sm">{mongoModal.title}</p>
              <button className="text-gray-400 hover:text-white text-xs" onClick={() => setMongoModal(null)}>✕ Close</button>
            </div>
            <pre className="text-[11px] text-green-300 font-mono whitespace-pre-wrap">
              {JSON.stringify(mongoModal.data, null, 2)}
            </pre>
          </div>
        </div>
      )}
    </>
  );
}

// ─── Fetcher ──────────────────────────────────────────────────────────────

const fetcher = (url: string) => fetch(url).then((r) => r.json());

function durationMs(start: string, end: string | null): string {
  if (!end) return "";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(2)}s`;
}

// ─── Page ────────────────────────────────────────────────────────────────

const kafdropUrl = process.env.NEXT_PUBLIC_KAFDROP_URL ?? "http://localhost:9000";
const grafanaUrl = process.env.NEXT_PUBLIC_GRAFANA_URL ?? "https://grafana.middleware.internal";
const mongoUrl = process.env.NEXT_PUBLIC_MONGO_URL ?? "https://mongo.middleware.internal";

export default function OpsPage() {
  const { issuanceId } = useParams<{ issuanceId: string }>();

  const { data: saga, error: sagaError, isLoading: sagaLoading } = useSWR<SagaRecord>(
    issuanceId ? `/api/policies/${issuanceId}` : null,
    fetcher,
    { refreshInterval: 1500 }
  );

  const { data: logsData, error: logsError } = useSWR<{ entries: LogEntry[]; error?: string }>(
    issuanceId ? `/api/loki?issuanceId=${issuanceId}&limit=60` : null,
    fetcher,
    { refreshInterval: 3000 }
  );

  const { data: flowData } = useSWR<{ events: FlowEvent[] }>(
    issuanceId ? `/api/policies/${issuanceId}/flow` : null,
    fetcher,
    { refreshInterval: 2000 }
  );
  const liveSteps = flowData?.events ?? [];
  const isLiveMode = liveSteps.length > 0;

  const sagaStatus: SagaStatus = saga?.status ?? "Initiated";
  const statusColor = STATUS_COLOR[sagaStatus] ?? "#94a3b8";
  const isRunning = saga && !["Completed", "Failed", "ComplianceBlocked"].includes(saga.status);
  const stepsToRender = isLiveMode ? flowEventsToSteps(liveSteps) : UC1_STEPS;
  const hasUc4LiveFlow = isUc4Flow(liveSteps);

  const completedSteps = stepsToRender.filter(
    (step) => computeStepStatus(step, sagaStatus, isLiveMode) === "done"
  ).length;

  return (
    <div className="space-y-6">
      {/* ── Header ──────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-start gap-4 justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <p className="text-xs font-mono" style={{ color: "var(--muted)" }}>Operations &amp; Observability</p>
            {isRunning && (
              <span className="text-xs animate-pulse" style={{ color: "#f59e0b" }}>● live</span>
            )}
          </div>
          <h1 className="text-2xl font-bold">EDA Flow Trace</h1>
          <code className="text-xs px-2 py-1 rounded font-mono mt-1 inline-block"
            style={{ background: "var(--border)", color: "var(--text)" }}>
            {issuanceId}
          </code>
        </div>
        {saga && (
          <div className="flex flex-wrap gap-3">
            <div className="text-right">
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Status</p>
              <span className="px-2 py-0.5 rounded text-xs font-semibold"
                style={{ background: statusColor + "22", color: statusColor, border: `1px solid ${statusColor}44` }}>
                {saga.status}
              </span>
            </div>
            <div className="text-right">
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Progress</p>
              <span className="text-sm font-mono">{completedSteps}/{stepsToRender.length} steps</span>
            </div>
            {saga.completedAt && (
              <div className="text-right">
                <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Duration</p>
                <span className="text-sm font-mono" style={{ color: "#22c55e" }}>
                  {durationMs(saga.requestedAt, saga.completedAt)}
                </span>
              </div>
            )}
          </div>
        )}
      </div>

      {sagaLoading && (
        <div className="text-sm animate-pulse" style={{ color: "var(--muted)" }}>Loading saga state…</div>
      )}
      {sagaError && !isLiveMode && (
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)" }}>
          Could not load saga: {sagaError.message}
        </div>
      )}

      {/* ── Main layout: sequence diagram full-width, sidebar below ───── */}
      {/* Sequence diagram gets the full container width so all lifelines  */}
      {/* are visible with minimal horizontal scrolling.                   */}
      <div className="space-y-6">

        {/* ── Sequence Diagram (full width) ────────────────────────────── */}
        <div className="rounded-lg border overflow-x-auto"
          style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
          <div className="px-4 pt-4 pb-2 flex items-center justify-between">
            <p className="text-sm font-semibold">Message Sequence</p>
            <p className="text-xs" style={{ color: "var(--muted)" }}>
              <span style={{ color: "#22c55e" }}>● done</span>
              <span className="mx-2" style={{ color: "#f59e0b" }}>● active</span>
              <span style={{ color: "var(--muted)" }}>● pending</span>
            </p>
          </div>
          <div className="px-2 pb-4">
            {!isLiveMode && !saga && !sagaLoading ? (
              <div className="py-12 text-center space-y-2">
                <p className="text-sm animate-pulse" style={{ color: "var(--muted)" }}>
                  ⏳ Waiting for trace data…
                </p>
                <p className="text-xs" style={{ color: "var(--muted)", opacity: 0.6 }}>
                  Logs are being collected from Loki. Refresh in a moment.
                </p>
              </div>
            ) : (
              <SequenceDiagram sagaStatus={sagaStatus} liveSteps={liveSteps} isLiveMode={isLiveMode} />
            )}
          </div>
          {/* Topic legend — shown for active steps */}
          <div className="border-t px-4 py-3 space-y-1" style={{ borderColor: "var(--border)" }}>
            <p className="text-xs font-semibold mb-2" style={{ color: "var(--muted)" }}>
              Topics / Queues
            </p>
            <div className="flex flex-wrap gap-2">
              {stepsToRender.map((step) => {
                const stepStatus = computeStepStatus(step, sagaStatus, isLiveMode);
                return (
                  <a
                    key={step.id}
                    href={`${kafdropUrl}/topic/${step.topic}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-xs font-mono px-2 py-0.5 rounded hover:opacity-100 transition-opacity"
                    style={{
                      background: "var(--bg)",
                      border: "1px solid var(--border)",
                      color: stepStatus === "done" ? "#22c55e" : "var(--muted)",
                      opacity: stepStatus === "pending" ? 0.4 : 1,
                    }}
                  >
                    {step.topic}
                  </a>
                );
              })}
            </div>
          </div>
        </div>

        {/* ── Below-diagram row: Saga panel + Observability links + Nav ── */}
        <div className="grid grid-cols-1 xl:grid-cols-[1fr_320px_220px] gap-6">

          {/* Saga state */}
          <div className="space-y-4">
            {saga && !hasUc4LiveFlow && <Uc1SagaPanel saga={saga} sagaStatus={sagaStatus} />}
            {hasUc4LiveFlow && <Uc4SagaPanel events={liveSteps} issuanceId={issuanceId ?? ""} />}
          </div>

          {/* Observability links */}
          <div className="rounded-lg border p-4 space-y-2"
            style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <p className="text-xs font-semibold mb-1" style={{ color: "var(--muted)" }}>
              Drill Deeper
            </p>
            {[
              {
                label: "All-service Logs (Loki) →",
                href: `${grafanaUrl}/explore?schemaVersion=1&queries=[{"datasource":{"type":"loki"},"expr":"{deployment_environment%3D%22local%22} |%3D \`${issuanceId}\`"}]`,
              },
              {
                label: "Service Logs (Loki) →",
                href: `${grafanaUrl}/explore?schemaVersion=1&queries=[{"datasource":{"type":"loki"},"expr":"{service_name%3D~%22.%2B%22} |%3D \`${issuanceId}\`"}]`,
              },
              {
                label: "Traces (Tempo) →",
                href: `${grafanaUrl}/explore?datasource=tempo`,
              },
              {
                label: "Kafka Topics (Kafdrop) →",
                href: kafdropUrl,
              },
              {
                label: "Saga State (Mongo) →",
                href: mongoUrl,
              },
            ].map(({ label, href }) => (
              <a key={label} href={href} target="_blank" rel="noopener noreferrer"
                className="block text-xs px-3 py-1.5 rounded border hover:text-white transition-colors"
                style={{ borderColor: "var(--border)", color: "var(--accent-light)" }}>
                {label}
              </a>
            ))}
          </div>

          {/* Nav links */}
          <div className="flex flex-col gap-2 text-xs">
            <Link href="/"
              className="text-center px-3 py-2 rounded border"
              style={{ borderColor: "var(--border)", color: "var(--muted)" }}>
              ← New Policy
            </Link>
            <Link href="/ops"
              className="text-center px-3 py-2 rounded border"
              style={{ borderColor: "var(--border)", color: "var(--muted)" }}>
              ← Ops Search
            </Link>
          </div>
        </div>
      </div>

      {/* ── Log Tail (Loki) ───────────────────────────────────────────── */}
      <div className="rounded-lg border"
        style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <div className="px-4 py-3 border-b flex items-center justify-between"
          style={{ borderColor: "var(--border)" }}>
          <p className="text-sm font-semibold">
            Live Log Tail
            <span className="ml-2 text-xs font-normal" style={{ color: "var(--muted)" }}>
              from Grafana Loki · all services · refreshes every 3s
            </span>
          </p>
          {logsData?.entries && (
            <span className="text-xs font-mono" style={{ color: "var(--muted)" }}>
              {logsData.entries.length} entries
            </span>
          )}
        </div>

        {logsError && (
          <div className="px-4 py-3 text-xs" style={{ color: "var(--muted)" }}>
            ⚠ Loki unavailable — check Docker stack. Raw logs visible in container stdout.
          </div>
        )}

        {logsData?.error && (
          <div className="px-4 py-3 text-xs" style={{ color: "var(--warning)" }}>
            ⚠ {logsData.error}
          </div>
        )}

        {!logsData && !logsError && (
          <div className="px-4 py-3 text-xs animate-pulse" style={{ color: "var(--muted)" }}>
            Querying Loki…
          </div>
        )}

        {logsData?.entries && logsData.entries.length === 0 && (
          <div className="px-4 py-3 text-xs" style={{ color: "var(--muted)" }}>
            No log entries found yet for this issuanceId. Logs appear within a few seconds of processing.
          </div>
        )}

        {logsData?.entries && logsData.entries.length > 0 && (
          <div className="overflow-x-auto font-mono text-xs"
            style={{ maxHeight: "360px", overflowY: "auto" }}>
            <table className="w-full border-collapse">
              <thead>
                <tr className="sticky top-0" style={{ background: "var(--surface)" }}>
                  {["Time", "Level", "Service", "Message"].map((h) => (
                    <th key={h} className="px-3 py-2 text-left font-semibold"
                      style={{ color: "var(--muted)", borderBottom: "1px solid var(--border)" }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {logsData.entries.slice(-50).reverse().map((entry, i) => {
                  const ls = LEVEL_STYLE[entry.level?.toUpperCase()] ?? LEVEL_STYLE.INFO;
                  return (
                    <tr key={i} className={clsx(i % 2 === 0 && "bg-black/10")}>
                      <td className="px-3 py-1 whitespace-nowrap" style={{ color: "var(--muted)" }}>
                        {formatTime(entry.ts)}
                      </td>
                      <td className="px-3 py-1 whitespace-nowrap font-bold" style={{ color: ls.color }}>
                        {ls.label}
                      </td>
                      <td className="px-3 py-1 whitespace-nowrap" style={{ color: "var(--accent-light)" }}>
                        {entry.service.replace(/-service$/, "")}
                      </td>
                      <td className="px-3 py-1" style={{ color: "var(--text)" }}>
                        {entry.message}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
