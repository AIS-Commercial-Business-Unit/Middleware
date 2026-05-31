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
  label: string;
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

const PARTICIPANTS: readonly Participant[] = [
  { id: "FileProcessing",   label: "File Processing",      color: "#b45309" },
  { id: "API",              label: "API Client",           color: "#6366f1" },
  { id: "PolicyIssuance",   label: "Policy Issuance",      color: "#0891b2" },
  { id: "Compliance",       label: "Plat. Compliance",     color: "#dc2626" },
  { id: "CustomerIdentity", label: "Customer Identity",    color: "#16a34a" },
  { id: "Integration",      label: "Plat. Integration",    color: "#7c3aed" },
  { id: "Billing",          label: "Billing Finance",      color: "#ea580c" },
  { id: "Notification",     label: "Notification",         color: "#0d9488" },
  { id: "PrsAppraisal",     label: "PRS Appraisal",        color: "#0891b2" },
  { id: "AtWork",           label: "AtWork SQL",           color: "#f59e0b" },
  { id: "Mainframe",        label: "Mainframe (MQ)",       color: "#8b5cf6" },
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

function buildVisibleParticipants(events: FlowEvent[], isLiveMode: boolean): Participant[] {
  if (!(isLiveMode && events.length > 0)) {
    const activeIds = new Set<string>(UC1_STEPS.flatMap((step) => [step.from, step.to]));
    return PARTICIPANTS.filter((participant) => activeIds.has(participant.id));
  }

  const participantIds = [...new Set([...events.map((event) => event.from), ...events.map((event) => event.to)])];
  let dynamicIndex = 0;

  return participantIds.map((id) => {
    const knownParticipant = PARTICIPANTS.find((participant) => participant.id === id);
    if (knownParticipant) {
      return knownParticipant;
    }

    const dynamicParticipant = createDynamicParticipant(id, dynamicIndex);
    dynamicIndex += 1;
    return dynamicParticipant;
  });
}

function getParticipantColor(id: string, participants: readonly Participant[]) {
  return participants.find((participant) => participant.id === id)?.color ?? getDynamicColor(id);
}

function flowEventsToSteps(events: FlowEvent[]): Step[] {
  return events.map((event, index) => ({
    id: `live-${index}`,
    from: event.from,
    to: event.to,
    msg: event.messageType,
    isEvent: true,
    topic: event.topic,
    doneAtLevel: 5,
    note: event.stack === "dotnet" ? "⬡ .NET" : undefined,
  }));
}

interface SequenceDiagramProps {
  sagaStatus: SagaStatus;
  liveSteps?: FlowEvent[];
  isLiveMode?: boolean;
}

// ─── SVG layout constants ─────────────────────────────────────────────────

const LEFT_MARGIN  = 76;
const COL_WIDTH    = 158;
const BOX_WIDTH    = 128;
const BOX_HEIGHT   = 44;
const HEADER_HEIGHT = 80;
const ROW_HEIGHT   = 50;
const BOTTOM_PAD   = 36;

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

// ─── Sequence Diagram ────────────────────────────────────────────────────

function SequenceDiagram({ sagaStatus, liveSteps = [], isLiveMode = false }: SequenceDiagramProps) {
  const stepsToRender = isLiveMode && liveSteps.length > 0 ? flowEventsToSteps(liveSteps) : UC1_STEPS;
  const visibleParticipants = buildVisibleParticipants(liveSteps, isLiveMode);

  const participantIndex = new Map<string, number>(visibleParticipants.map((participant, index) => [participant.id, index]));
  const svgRef = useRef<SVGSVGElement>(null);
  const [tooltip, setTooltip] = useState<{
    step: Step | FlowEvent | null;
    x: number;
    y: number;
  } | null>(null);
  const svgWidth = LEFT_MARGIN + visibleParticipants.length * COL_WIDTH + 20;
  const svgHeight = HEADER_HEIGHT + stepsToRender.length * ROW_HEIGHT + BOTTOM_PAD;
  const lifelineBottom = svgHeight - BOTTOM_PAD;

  return (
    <div>
      <div className="flex items-center gap-2 mb-2">
        <span
          className="text-xs px-2 py-0.5 rounded-full font-semibold"
          style={{
            background: isLiveMode ? "#16a34a22" : "#6366f122",
            color: isLiveMode ? "#4ade80" : "#818cf8",
            border: `1px solid ${isLiveMode ? "#16a34a" : "#6366f1"}`,
          }}
        >
          {isLiveMode ? "📡 Live — from Loki" : "📋 Static UC1 reference"}
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
            {visibleParticipants.map((p) => (
              <marker
                key={`arrow-${p.id}`}
                id={`arrow-${p.id}`}
                markerWidth="9"
                markerHeight="9"
                refX="8"
                refY="3"
                orient="auto"
              >
                <path d="M0,0 L0,6 L8,3 z" fill={p.color} />
              </marker>
            ))}
            <marker id="arrow-pending" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">
              <path d="M0,0 L0,6 L8,3 z" fill="#4b5563" />
            </marker>
          </defs>

          {/* Row stripes */}
          {stepsToRender.map((_, i) =>
            i % 2 === 0 ? (
              <rect
                key={`stripe-${i}`}
                x={0}
                y={HEADER_HEIGHT + i * ROW_HEIGHT}
                width={svgWidth}
                height={ROW_HEIGHT}
                fill="rgba(255,255,255,0.018)"
              />
            ) : null
          )}

          {/* Participant headers */}
          {visibleParticipants.map((p, i) => {
            const cx = colX(i);
            return (
              <g key={`hdr-${p.id}`}>
                <rect x={cx - BOX_WIDTH / 2} y={10} width={BOX_WIDTH} height={BOX_HEIGHT} rx={6} fill={p.color} />
                <text x={cx} y={30} textAnchor="middle" fill="white" fontSize={12} fontWeight="700">{p.id}</text>
                <text x={cx} y={46} textAnchor="middle" fill="rgba(255,255,255,0.72)" fontSize={9}>{p.label}</text>
              </g>
            );
          })}

          {/* Lifelines */}
          {visibleParticipants.map((p, i) => (
            <line
              key={`ll-${p.id}`}
              x1={colX(i)}
              y1={HEADER_HEIGHT}
              x2={colX(i)}
              y2={lifelineBottom}
              stroke="#2d3148"
              strokeWidth={1}
              strokeDasharray="4 4"
            />
          ))}

          {/* Messages */}
          {stepsToRender.map((step, i) => {
            const rowY = HEADER_HEIGHT + i * ROW_HEIGHT + ROW_HEIGHT / 2;
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
            const lineEnd = toX - direction * 1;
            const markerId = status === "pending" ? "arrow-pending" : `arrow-${step.from}`;
            const dash = step.isEvent ? "6 3" : undefined;
            const dotFill = status === "done" ? "#22c55e22" : status === "active" ? "#f59e0b22" : "#1f2937";
            const dotStroke = status === "done" ? "#22c55e" : status === "active" ? "#f59e0b" : "#374151";
            const hoverStep = isLiveMode ? liveSteps[i] ?? step : step;

            return (
              <g
                key={step.id}
                opacity={opacity}
                onMouseEnter={(e) => {
                  const svgRect = svgRef.current?.getBoundingClientRect();
                  setTooltip({
                    step: hoverStep,
                    x: e.clientX - (svgRect?.left ?? 0),
                    y: e.clientY - (svgRect?.top ?? 0),
                  });
                }}
                onMouseMove={(e) => {
                  const svgRect = svgRef.current?.getBoundingClientRect();
                  setTooltip({
                    step: hoverStep,
                    x: e.clientX - (svgRect?.left ?? 0),
                    y: e.clientY - (svgRect?.top ?? 0),
                  });
                }}
                onMouseLeave={() => setTooltip(null)}
                style={{ cursor: "pointer" }}
              >
                <circle cx={LEFT_MARGIN - 24} cy={rowY} r={6} fill={dotFill} stroke={dotStroke} strokeWidth={1.5} />
                {status === "done" && (
                  <text x={LEFT_MARGIN - 24} y={rowY + 4} textAnchor="middle" fontSize={8} fill="#22c55e">✓</text>
                )}

                <text x={LEFT_MARGIN - 10} y={rowY + 4} textAnchor="end" fontSize={10} fill="#6b7280">{i + 1}</text>

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

                {step.note && (
                  <text x={(fromX + toX) / 2} y={rowY + 13} textAnchor="middle" fontSize={8} fill="#94a3b8">
                    {step.note}
                  </text>
                )}
              </g>
            );
          })}
        </svg>

        {tooltip?.step && (
          <div
            style={{
              position: "absolute",
              left: Math.min(tooltip.x + 12, svgWidth - 260),
              top: Math.max(tooltip.y - 8, 8),
              zIndex: 50,
              pointerEvents: "none",
            }}
            className="bg-gray-900 border border-gray-600 rounded-lg shadow-xl p-3 max-w-xs text-xs"
          >
            <div className="font-semibold text-white mb-1">
              {"messageType" in tooltip.step ? tooltip.step.messageType : tooltip.step.msg}
            </div>
            {"details" in tooltip.step && tooltip.step.details ? (
              <>
                <div className="text-gray-300 mb-2 leading-relaxed">
                  {tooltip.step.details.description}
                </div>
                <div className="space-y-0.5 text-gray-400 font-mono text-[10px]">
                  <div>📡 topic: {tooltip.step.details.topic || tooltip.step.topic || "—"}</div>
                  <div>↕ {tooltip.step.details.direction}</div>
                  <div>⚙ {tooltip.step.details.stack}</div>
                  <div>🕐 {new Date(tooltip.step.details.timestamp).toLocaleTimeString()}</div>
                </div>
              </>
            ) : (
              <div className="space-y-0.5 text-gray-400 font-mono text-[10px]">
                <div>📡 {tooltip.step.topic || "—"}</div>
                <div>🔀 {"msg" in tooltip.step ? (tooltip.step.isEvent ? "event" : "command") : "event"}</div>
              </div>
            )}
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

// ─── Fetcher ──────────────────────────────────────────────────────────────

const fetcher = (url: string) => fetch(url).then((r) => r.json());

function durationMs(start: string, end: string | null): string {
  if (!end) return "";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(2)}s`;
}

// ─── Page ────────────────────────────────────────────────────────────────

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
          <h1 className="text-2xl font-bold">IssuanceSaga Flow</h1>
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
      {sagaError && (
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)" }}>
          Could not load saga: {sagaError.message}
        </div>
      )}

      {/* ── Main two-column layout ───────────────────────────────────── */}
      <div className="grid grid-cols-1 xl:grid-cols-[1fr_320px] gap-6">

        {/* ── Sequence Diagram ─────────────────────────────────────── */}
        <div className="rounded-lg border overflow-x-auto"
          style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
          <div className="px-4 pt-4 pb-2 flex items-center justify-between">
            <p className="text-sm font-semibold">Kafka Message Sequence</p>
            <p className="text-xs" style={{ color: "var(--muted)" }}>
              <span style={{ color: "#22c55e" }}>● done</span>
              <span className="mx-2" style={{ color: "#f59e0b" }}>● active</span>
              <span style={{ color: "var(--muted)" }}>● pending</span>
            </p>
          </div>
          <div className="px-2 pb-4">
            <SequenceDiagram sagaStatus={sagaStatus} liveSteps={liveSteps} isLiveMode={isLiveMode} />
          </div>
          {/* Topic legend — shown for active steps */}
          <div className="border-t px-4 py-3 space-y-1" style={{ borderColor: "var(--border)" }}>
            <p className="text-xs font-semibold mb-2" style={{ color: "var(--muted)" }}>
              Kafka Topics
            </p>
            <div className="flex flex-wrap gap-2">
              {stepsToRender.map((step) => {
                const stepStatus = computeStepStatus(step, sagaStatus, isLiveMode);
                return (
                  <a
                    key={step.id}
                    href={`http://localhost:9000/topic/${step.topic}`}
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

        {/* ── Right sidebar: Saga state ────────────────────────────── */}
        <div className="space-y-4">

          {/* Saga details card */}
          {saga && (
            <div className="rounded-lg border p-4 space-y-3"
              style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
              <p className="text-sm font-semibold">Saga Details</p>
              {[
                { label: "Account",    value: saga.accountId },
                { label: "Channel",    value: saga.submittingChannel },
                { label: "PolicyType", value: String(saga.policyTypeCode) },
                { label: "PAS",        value: saga.targetPas ?? "—" },
                { label: "Policy #",   value: saga.policyNumbers?.join(", ") ?? "—" },
                { label: "Started",    value: new Date(saga.requestedAt).toLocaleTimeString() },
                { label: "Ended",      value: saga.completedAt ? new Date(saga.completedAt).toLocaleTimeString() : "—" },
              ].map(({ label, value }) => (
                <div key={label} className="flex justify-between gap-2 text-xs">
                  <span style={{ color: "var(--muted)" }}>{label}</span>
                  <span className="font-mono text-right truncate" style={{ color: "var(--text)", maxWidth: "180px" }}>
                    {value}
                  </span>
                </div>
              ))}

              {saga.failureReason && (
                <div className="rounded px-2 py-1.5 text-xs" style={{ background: "#2d1515", color: "var(--danger)" }}>
                  {saga.failureReason}
                </div>
              )}

              {/* Parallel branches */}
              {statusLevel(sagaStatus) >= 4 && (
                <div className="space-y-2 pt-1 border-t" style={{ borderColor: "var(--border)" }}>
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
          )}

          {/* Observability links */}
          <div className="rounded-lg border p-4 space-y-2"
            style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <p className="text-xs font-semibold mb-1" style={{ color: "var(--muted)" }}>
              Drill Deeper
            </p>
            {[
              {
                label: "All-service Logs (Loki) →",
                href: `http://localhost:3001/explore?schemaVersion=1&queries=[{"datasource":{"type":"loki"},"expr":"{deployment_environment%3D%22local%22} |%3D \`${issuanceId}\`"}]`,
              },
              {
                label: "Policy Issuance Logs →",
                href: `http://localhost:3001/explore?schemaVersion=1&queries=[{"datasource":{"type":"loki"},"expr":"{service_name%3D%22policy-issuance-service%22} |%3D \`${issuanceId}\`"}]`,
              },
              {
                label: "Traces (Tempo) →",
                href: `http://localhost:3001/explore?datasource=tempo`,
              },
              {
                label: "Kafka Topics (Kafdrop) →",
                href: "http://localhost:9000",
              },
              {
                label: "Saga State (Mongo) →",
                href: "http://localhost:8888",
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
          <div className="flex gap-2 text-xs">
            <Link href="/"
              className="flex-1 text-center px-3 py-2 rounded border"
              style={{ borderColor: "var(--border)", color: "var(--muted)" }}>
              ← New Policy
            </Link>
            <Link href="/ops"
              className="flex-1 text-center px-3 py-2 rounded border"
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
