"use client";

import { useParams } from "next/navigation";
import useSWR from "swr";
import clsx from "clsx";

const grafanaUrl = process.env.NEXT_PUBLIC_GRAFANA_URL ?? "http://localhost:3001";

const fetcher = (url: string) => fetch(url).then((r) => r.json());

const STATUS_COLOR: Record<string, string> = {
  Initiated: "#6366f1",
  AwaitingCompliance: "#f59e0b",
  AwaitingAccountRecord: "#f59e0b",
  AwaitingPAS: "#f59e0b",
  PASConfirmed: "#06b6d4",
  BillingAssociated: "#06b6d4",
  CustomerUpdateComplete: "#06b6d4",
  Completed: "#22c55e",
  Failed: "#ef4444",
  ComplianceBlocked: "#ef4444",
};

const SAGA_STEPS = [
  { key: "Initiated", label: "Saga Created", description: "IssuePolicy received, IssuanceSaga started" },
  { key: "AwaitingCompliance", label: "Compliance Check", description: "RequestComplianceCheck sent to Platform.Compliance" },
  { key: "AwaitingAccountRecord", label: "Account Lookup", description: "ComplianceCleared — GetOrCreateAccountServiceRecord sent" },
  { key: "AwaitingPAS", label: "PAS Dispatch", description: "IssuePolicyRequested routed via Content-Based Router" },
  { key: "PASConfirmed", label: "PAS Confirmed", description: "PolicyAdminSystemResponseReceived — parallel branches started" },
  { key: "Completed", label: "Policy Issued", description: "Saga join complete — PolicyIssued published" },
];

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

function StatusBadge({ status }: { status: string }) {
  const color = STATUS_COLOR[status] ?? "#94a3b8";
  return (
    <span
      className="px-2 py-0.5 rounded text-xs font-semibold"
      style={{ background: color + "22", color, border: `1px solid ${color}44` }}
    >
      {status}
    </span>
  );
}

export default function SagaExplorerPage() {
  const { issuanceId } = useParams<{ issuanceId: string }>();
  const { data, error, isLoading } = useSWR<SagaRecord>(
    issuanceId ? `/api/policies/${issuanceId}` : null,
    fetcher,
    { refreshInterval: 1500 } // Poll every 1.5s while saga is in progress
  );

  const currentStepIndex = data
    ? SAGA_STEPS.findIndex((s) => s.key === data.status)
    : -1;

  return (
    <div className="space-y-8">
      <div>
        <p className="text-xs font-mono mb-1" style={{ color: "var(--muted)" }}>
          Saga Explorer
        </p>
        <h1 className="text-2xl font-bold mb-1">IssuanceSaga</h1>
        <code className="text-xs px-2 py-1 rounded font-mono" style={{ background: "var(--border)", color: "var(--text)" }}>
          {issuanceId}
        </code>
      </div>

      {isLoading && (
        <div className="text-sm animate-pulse" style={{ color: "var(--muted)" }}>
          Loading saga state…
        </div>
      )}

      {error && (
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)" }}>
          Failed to load saga: {error.message}
        </div>
      )}

      {data && (
        <>
          {/* Status header */}
          <div className="rounded-lg border p-5 flex flex-wrap gap-6" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <div>
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Status</p>
              <StatusBadge status={data.status} />
            </div>
            <div>
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Account</p>
              <code className="text-sm font-mono">{data.accountId}</code>
            </div>
            <div>
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Channel</p>
              <span className="text-sm">{data.submittingChannel}</span>
            </div>
            <div>
              <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>PolicyTypeCode</p>
              <span className="text-sm font-mono">{data.policyTypeCode}</span>
            </div>
            {data.targetPas && (
              <div>
                <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>PAS Routed To</p>
                <span className="text-sm font-mono">{data.targetPas}</span>
              </div>
            )}
            {data.policyNumbers && data.policyNumbers.length > 0 && (
              <div>
                <p className="text-xs mb-1" style={{ color: "var(--muted)" }}>Policy Numbers</p>
                <span className="text-sm font-mono">{data.policyNumbers.join(", ")}</span>
              </div>
            )}
            {data.failureReason && (
              <div className="w-full">
                <p className="text-xs mb-1" style={{ color: "var(--danger)" }}>Failure Reason</p>
                <span className="text-sm">{data.failureReason}</span>
              </div>
            )}
          </div>

          {/* State machine progress */}
          <div className="rounded-lg border p-5 space-y-3" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <p className="text-sm font-semibold mb-4">IssuanceSaga State Machine</p>
            {SAGA_STEPS.map((step, i) => {
              const isDone = currentStepIndex > i || data.status === step.key;
              const isCurrent = data.status === step.key;
              const isFailed = data.status === "Failed" || data.status === "ComplianceBlocked";
              return (
                <div key={step.key} className="flex items-start gap-3">
                  <div
                    className="w-6 h-6 rounded-full flex-shrink-0 flex items-center justify-center text-xs font-bold mt-0.5"
                    style={{
                      background: isFailed && isCurrent ? "#ef444422" : isDone ? "#22c55e22" : "var(--border)",
                      border: `1px solid ${isFailed && isCurrent ? "#ef4444" : isDone ? "#22c55e" : "var(--border)"}`,
                      color: isFailed && isCurrent ? "#ef4444" : isDone ? "#22c55e" : "var(--muted)",
                    }}
                  >
                    {isDone && !isCurrent ? "✓" : i + 1}
                  </div>
                  <div className="flex-1">
                    <p
                      className={clsx("text-sm font-medium", { "opacity-40": !isDone && !isCurrent })}
                      style={{ color: isCurrent ? "var(--text)" : isDone ? "var(--muted)" : "var(--muted)" }}
                    >
                      {step.label}
                      {isCurrent && !isFailed && (
                        <span className="ml-2 text-xs animate-pulse" style={{ color: "var(--accent-light)" }}>
                          ● in progress
                        </span>
                      )}
                    </p>
                    <p className="text-xs" style={{ color: "var(--muted)" }}>{step.description}</p>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Parallel branches */}
          {(currentStepIndex >= 4 || data.status === "Completed") && (
            <div className="rounded-lg border p-5 space-y-3" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
              <p className="text-sm font-semibold">Parallel Branches (Saga Join)</p>
              <p className="text-xs mb-3" style={{ color: "var(--muted)" }}>
                Both must complete before PolicyIssued is published (BR-PIL-003)
              </p>
              <div className="grid grid-cols-2 gap-4">
                <div className="rounded border p-3" style={{ borderColor: data.billingComplete ? "#22c55e44" : "var(--border)" }}>
                  <p className="text-xs font-semibold mb-1">Billing Association</p>
                  <StatusBadge status={data.billingComplete ? "Completed" : "AwaitingPAS"} />
                </div>
                <div className="rounded border p-3" style={{ borderColor: data.customerUpdateComplete ? "#22c55e44" : "var(--border)" }}>
                  <p className="text-xs font-semibold mb-1">Customer Record Update</p>
                  <StatusBadge status={data.customerUpdateComplete ? "Completed" : "AwaitingPAS"} />
                </div>
              </div>
            </div>
          )}

          {/* Links to observability */}
          <div className="rounded-lg border p-4 flex flex-wrap gap-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <p className="text-xs w-full font-semibold" style={{ color: "var(--muted)" }}>Drill Deeper</p>
            <a
              href={`${grafanaUrl}/explore?schemaVersion=1&queries=[{"datasource":{"type":"loki"},"expr":"{service_name%3D\"policy-issuance-service\"} |= \`${issuanceId}\`"}]`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs px-3 py-1.5 rounded border transition-colors hover:text-white"
              style={{ borderColor: "var(--border)", color: "var(--accent-light)" }}
            >
              View Logs in Grafana Loki →
            </a>
            <a
              href={`${grafanaUrl}/explore?datasource=tempo`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs px-3 py-1.5 rounded border transition-colors hover:text-white"
              style={{ borderColor: "var(--border)", color: "var(--accent-light)" }}
            >
              View Traces in Grafana Tempo →
            </a>
          </div>
        </>
      )}
    </div>
  );
}
