"use client";

import { useParams } from "next/navigation";
import useSWR from "swr";
import clsx from "clsx";
import Link from "next/link";

// ─── Types ────────────────────────────────────────────────────────────────────

type FileBatchStatus =
  | "Received"
  | "Parsing"
  | "Processing"
  | "Completed"
  | "PartialFailure"
  | "Failed"
  | "TimedOut";

interface FileBatch {
  batchId: string;
  fileName: string;
  dropZoneName: string;
  status: FileBatchStatus;
  totalRecords: number | null;
  processedRecords: number;
  succeededRecords: number;
  failedRecords: number;
  percentComplete: number;
  receivedAt: string;
  processingCompletedAt: string | null;
}

type BatchRecordStatus = "Pending" | "Processing" | "Succeeded" | "Failed" | "DeadLettered";

interface BatchRecord {
  recordId: string;
  batchId: string;
  sequenceNumber: number;
  rawContent: string;
  status: BatchRecordStatus;
  retryCount: number;
  processorResult: string | null;
  processedAt: string | null;
  correlationId: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const TERMINAL_STATUSES: FileBatchStatus[] = ["Completed", "PartialFailure", "Failed", "TimedOut"];

const fetcher = (url: string) => fetch(url).then((r) => r.json());

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function formatDuration(start: string, end: string) {
  const ms = new Date(end).getTime() - new Date(start).getTime();
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  return rem > 0 ? `${m}m ${rem}s` : `${m}m`;
}

// ─── Status Badges ────────────────────────────────────────────────────────────

const BATCH_STATUS_STYLES: Record<FileBatchStatus, { label: string; classes: string }> = {
  Received:      { label: "Received",        classes: "bg-gray-400/10 text-gray-400" },
  Parsing:       { label: "Parsing",         classes: "bg-blue-400/10 text-blue-400" },
  Processing:    { label: "Processing",      classes: "bg-yellow-400/10 text-yellow-400" },
  Completed:     { label: "Completed",       classes: "bg-green-400/10 text-green-400" },
  PartialFailure:{ label: "Partial Failure", classes: "bg-orange-400/10 text-orange-400" },
  Failed:        { label: "Failed",          classes: "bg-red-400/10 text-red-400" },
  TimedOut:      { label: "Timed Out",       classes: "bg-red-400/10 text-red-400" },
};

const RECORD_STATUS_STYLES: Record<BatchRecordStatus, { label: string; classes: string }> = {
  Pending:      { label: "Pending",      classes: "bg-gray-400/10 text-gray-400" },
  Processing:   { label: "Processing",   classes: "bg-blue-400/10 text-blue-400" },
  Succeeded:    { label: "Succeeded ✓",  classes: "bg-green-400/10 text-green-400" },
  Failed:       { label: "Failed",       classes: "bg-orange-400/10 text-orange-400" },
  DeadLettered: { label: "Dead Lettered",classes: "bg-red-400/10 text-red-400" },
};

function BatchStatusBadge({ status }: { status: FileBatchStatus }) {
  const s = BATCH_STATUS_STYLES[status] ?? { label: status, classes: "bg-gray-400/10 text-gray-400" };
  return (
    <span className={clsx("inline-flex items-center px-2.5 py-1 rounded text-sm font-semibold", s.classes)}>
      {s.label}
    </span>
  );
}

function RecordStatusBadge({ status }: { status: BatchRecordStatus }) {
  const s = RECORD_STATUS_STYLES[status] ?? { label: status, classes: "bg-gray-400/10 text-gray-400" };
  return (
    <span className={clsx("inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold", s.classes)}>
      {s.label}
    </span>
  );
}

// ─── Stat Box ─────────────────────────────────────────────────────────────────

function StatBox({ label, value, color }: { label: string; value: number | string; color?: string }) {
  return (
    <div
      className="rounded-lg p-4 space-y-1"
      style={{ background: "var(--bg)", border: "1px solid var(--border)" }}
    >
      <p className="text-xs font-medium uppercase tracking-wide" style={{ color: "var(--muted)" }}>
        {label}
      </p>
      <p className={clsx("text-2xl font-bold tabular-nums", color ?? "text-white")}>{value}</p>
    </div>
  );
}

// ─── Policy Numbers helper ────────────────────────────────────────────────────

function parsePolicyNumbers(result: string | null): string {
  if (!result) return "";
  try {
    const parsed = JSON.parse(result);
    if (Array.isArray(parsed)) return parsed.join(", ");
    if (parsed.policyNumbers) return (parsed.policyNumbers as string[]).join(", ");
    if (parsed.policyNumber) return String(parsed.policyNumber);
  } catch {
    // not JSON
  }
  return result.length > 60 ? result.slice(0, 60) + "…" : result;
}

function parseFailureReason(result: string | null): string {
  if (!result) return "";
  try {
    const parsed = JSON.parse(result);
    if (parsed.error) return String(parsed.error);
    if (parsed.reason) return String(parsed.reason);
    if (parsed.message) return String(parsed.message);
  } catch {
    // not JSON
  }
  return result.length > 80 ? result.slice(0, 80) + "…" : result;
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function BatchDetailPage() {
  const { batchId } = useParams<{ batchId: string }>();

  const { data: batch, error: batchError } = useSWR<FileBatch>(
    batchId ? `/api/file-processing/batches/${batchId}` : null,
    fetcher,
    {
      refreshInterval: (data) =>
        data && TERMINAL_STATUSES.includes(data.status) ? 0 : 2000,
    }
  );

  const { data: records, error: recordsError } = useSWR<BatchRecord[]>(
    batchId ? `/api/file-processing/batches/${batchId}/records` : null,
    fetcher,
    {
      refreshInterval: (data) => {
        if (!batch) return 2000;
        if (TERMINAL_STATUSES.includes(batch.status)) return 0;
        // stop if all records are terminal
        if (data && data.every((r) => r.status === "Succeeded" || r.status === "Failed" || r.status === "DeadLettered")) return 0;
        return 2000;
      },
    }
  );

  // ─── Loading ───────────────────────────────────────────────────────────────

  if (!batch && !batchError) {
    return (
      <div className="space-y-4">
        <Link href="/batches" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
          ← Back to Batches
        </Link>
        <div className="flex items-center gap-2 text-sm" style={{ color: "var(--muted)" }}>
          <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
          </svg>
          Loading batch…
        </div>
      </div>
    );
  }

  if (batchError || !batch) {
    return (
      <div className="space-y-4">
        <Link href="/batches" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
          ← Back to Batches
        </Link>
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>
          Failed to load batch: {batchError?.message ?? "Not found"}
        </div>
      </div>
    );
  }

  const pct = batch.totalRecords
    ? Math.round((batch.processedRecords / batch.totalRecords) * 100)
    : Math.round(batch.percentComplete ?? 0);
  const inProgress = (batch.totalRecords ?? 0) - batch.succeededRecords - batch.failedRecords;
  const isTerminal = TERMINAL_STATUSES.includes(batch.status);

  return (
    <div className="space-y-8">
      {/* Back nav */}
      <Link href="/batches" className="inline-flex items-center text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
        ← Back to Batches
      </Link>

      {/* Header */}
      <div className="rounded-lg border p-6 space-y-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold font-mono break-all">{batch.fileName}</h1>
            <p className="text-sm mt-1" style={{ color: "var(--muted)" }}>
              Received {formatDateTime(batch.receivedAt)}
              {isTerminal && batch.processingCompletedAt && (
                <> · Duration: <strong>{formatDuration(batch.receivedAt, batch.processingCompletedAt)}</strong></>
              )}
            </p>
            <p className="text-xs mt-1 font-mono" style={{ color: "var(--muted)" }}>
              Batch ID: {batch.batchId}
            </p>
          </div>
          <BatchStatusBadge status={batch.status} />
        </div>

        {/* Stat boxes */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <StatBox label="Total" value={batch.totalRecords ?? "—"} />
          <StatBox label="Succeeded" value={batch.succeededRecords} color="text-green-400" />
          <StatBox label="Failed" value={batch.failedRecords} color="text-red-400" />
          <StatBox label="In Progress" value={Math.max(0, inProgress)} color="text-yellow-400" />
        </div>

        {/* Progress bar */}
        {batch.totalRecords !== null && (
          <div className="space-y-1.5">
            <div className="w-full bg-gray-800 rounded-full h-2">
              <div
                className={clsx(
                  "h-2 rounded-full transition-all duration-500",
                  batch.status === "Completed" ? "bg-green-500"
                  : batch.status === "PartialFailure" ? "bg-orange-500"
                  : batch.status === "Failed" ? "bg-red-500"
                  : "bg-blue-500"
                )}
                style={{ width: `${pct}%` }}
              />
            </div>
            <p className="text-xs" style={{ color: "var(--muted)" }}>
              {batch.processedRecords} / {batch.totalRecords} ({pct}%)
            </p>
          </div>
        )}
      </div>

      {/* Records table */}
      <div className="rounded-lg border p-6 space-y-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <h2 className="text-lg font-semibold">
          Records
          {records && (
            <span className="ml-2 text-sm font-normal" style={{ color: "var(--muted)" }}>
              ({records.length} shown)
            </span>
          )}
        </h2>

        {recordsError && (
          <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>
            Failed to load records
          </div>
        )}

        {!records && !recordsError && (
          <div className="flex items-center gap-2 text-sm" style={{ color: "var(--muted)" }}>
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
            </svg>
            Loading records…
          </div>
        )}

        {records && records.length === 0 && (
          <p className="text-sm py-4 text-center" style={{ color: "var(--muted)" }}>
            No records found for this batch.
          </p>
        )}

        {records && records.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="text-left" style={{ borderBottom: "1px solid var(--border)", color: "var(--muted)" }}>
                  <th className="py-2 pr-3 font-medium w-10">#</th>
                  <th className="py-2 pr-3 font-medium">Record ID</th>
                  <th className="py-2 pr-3 font-medium">Status</th>
                  <th className="py-2 pr-3 font-medium">Policy Numbers / Failure Reason</th>
                  <th className="py-2 font-medium">Processed At</th>
                </tr>
              </thead>
              <tbody>
                {records.slice(0, 100).map((record) => (
                  <tr key={record.recordId} className="border-t" style={{ borderColor: "var(--border)" }}>
                    <td className="py-2.5 pr-3 font-mono text-xs" style={{ color: "var(--muted)" }}>
                      {record.sequenceNumber}
                    </td>
                    <td className="py-2.5 pr-3 font-mono text-xs max-w-[120px] truncate" title={record.recordId}>
                      {record.recordId.slice(0, 8)}…
                    </td>
                    <td className="py-2.5 pr-3">
                      <RecordStatusBadge status={record.status} />
                    </td>
                    <td className="py-2.5 pr-3 text-xs max-w-[260px]">
                      {record.status === "Succeeded" && record.processorResult ? (
                        <span className="text-green-400 font-mono">
                          {parsePolicyNumbers(record.processorResult)}
                        </span>
                      ) : (record.status === "Failed" || record.status === "DeadLettered") && record.processorResult ? (
                        <span className="text-orange-400" title={record.processorResult}>
                          {parseFailureReason(record.processorResult)}
                        </span>
                      ) : (
                        <span style={{ color: "var(--muted)" }}>—</span>
                      )}
                    </td>
                    <td className="py-2.5 text-xs" style={{ color: "var(--muted)" }}>
                      {record.processedAt ? formatDateTime(record.processedAt) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
