"use client";

import { useState } from "react";
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

// ─── Helpers ──────────────────────────────────────────────────────────────────

const fetcher = (url: string) => fetch(url).then((r) => r.json());

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

const STATUS_STYLES: Record<FileBatchStatus, { label: string; classes: string }> = {
  Received:      { label: "Received",       classes: "bg-gray-400/10 text-gray-400" },
  Parsing:       { label: "Parsing",        classes: "bg-blue-400/10 text-blue-400" },
  Processing:    { label: "Processing",     classes: "bg-yellow-400/10 text-yellow-400" },
  Completed:     { label: "Completed",      classes: "bg-green-400/10 text-green-400" },
  PartialFailure:{ label: "Partial Failure",classes: "bg-orange-400/10 text-orange-400" },
  Failed:        { label: "Failed",         classes: "bg-red-400/10 text-red-400" },
  TimedOut:      { label: "Timed Out",      classes: "bg-red-400/10 text-red-400" },
};

function StatusBadge({ status }: { status: FileBatchStatus }) {
  const s = STATUS_STYLES[status] ?? { label: status, classes: "bg-gray-400/10 text-gray-400" };
  return (
    <span className={clsx("inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold", s.classes)}>
      {s.label}
    </span>
  );
}

// ─── Sample CSV Viewer ────────────────────────────────────────────────────────

const SAMPLE_CSV = `PolicyNumber,ExpirationDate,InsuredName,PolicyTypeCode,PolicyTypeSubCode,PremiumAmount,ProducerCode,BillingType,AccountId
POL-ACME-001,2025-09-15,Acme Corp,1,1,4250.00,PROD-001,DirectBill,ACC-ACME-001
POL-GLOBEX-002,2025-08-30,Globex Corporation,42,1,8900.00,PROD-002,Agency,ACC-GLOBEX-002
POL-INITECH-003,2025-10-01,Initech LLC,10,2,3100.00,PROD-003,DirectBill,ACC-INITECH-003`;

function SampleCsvViewer() {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button
        onClick={() => setOpen((v) => !v)}
        className="text-xs font-medium flex items-center gap-1 transition-colors hover:text-white"
        style={{ color: "var(--accent-light)" }}
      >
        <span>{open ? "▼" : "▶"}</span>
        View Sample CSV Format
      </button>
      {open && (
        <div className="mt-3 rounded border overflow-x-auto" style={{ borderColor: "var(--border)" }}>
          <div className="px-3 py-1.5 text-xs font-medium border-b flex items-center justify-between"
               style={{ borderColor: "var(--border)", color: "var(--muted)", background: "var(--bg)" }}>
            <span>RENEWAL_*.csv — 9 columns, header required</span>
          </div>
          <pre className="p-3 text-xs font-mono leading-relaxed overflow-x-auto"
               style={{ background: "var(--bg)", color: "var(--text)", whiteSpace: "pre" }}>
            {SAMPLE_CSV}
          </pre>
        </div>
      )}
    </div>
  );
}

// ─── Generate Section ─────────────────────────────────────────────────────────

interface GenerateResult {
  fileName?: string;
  message?: string;
  error?: string;
}

function GenerateSection({ onSuccess }: { onSuccess: () => void }) {
  const [count, setCount] = useState(10);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<GenerateResult | null>(null);

  async function generate() {
    setLoading(true);
    setResult(null);
    try {
      const res = await fetch(`/api/file-processing/batches/generate?count=${count}`, {
        method: "POST",
      });
      const data = await res.json();
      if (!res.ok) {
        setResult({ error: data.error ?? `HTTP ${res.status}` });
      } else {
        setResult({ fileName: data.fileName ?? data.batchId ?? "batch created", message: data.message });
        onSuccess();
      }
    } catch (err: unknown) {
      setResult({ error: err instanceof Error ? err.message : "Unknown error" });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="rounded-lg border p-6 space-y-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
      <h2 className="text-lg font-semibold">Generate &amp; Run Sample Batch</h2>
      <div className="flex items-end gap-4">
        <div className="space-y-1.5">
          <label className="block text-sm font-medium">Record Count</label>
          <input
            type="number"
            min={1}
            max={500}
            value={count}
            onChange={(e) => setCount(Math.min(500, Math.max(1, Number(e.target.value))))}
            className="w-28 rounded px-3 py-2 text-sm font-mono"
            style={{ background: "var(--bg)", border: "1px solid var(--border)", color: "var(--text)" }}
          />
        </div>
        <button
          onClick={generate}
          disabled={loading}
          className="rounded px-4 py-2 text-sm font-semibold transition-opacity disabled:opacity-50"
          style={{ background: "var(--accent)", color: "white" }}
        >
          {loading ? (
            <span className="flex items-center gap-2">
              <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
              </svg>
              Generating…
            </span>
          ) : (
            "Generate Renewal Batch →"
          )}
        </button>
      </div>

      <SampleCsvViewer />

      {result?.error && (
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>
          {result.error}
        </div>
      )}
      {result?.fileName && (
        <div className="rounded px-3 py-2 text-sm" style={{ background: "#102010", color: "var(--success)", border: "1px solid var(--success)" }}>
          ✓ Batch created: <span className="font-mono">{result.fileName}</span>
          {result.message && <span className="ml-2 opacity-70">{result.message}</span>}
        </div>
      )}
    </div>
  );
}

// ─── Batch List ───────────────────────────────────────────────────────────────

function BatchList() {
  const { data, error, mutate } = useSWR<FileBatch[]>("/api/file-processing/batches", fetcher, {
    refreshInterval: 2000,
  });

  if (error) {
    return (
      <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>
        Failed to load batches: {error.message ?? "Service unavailable"}
      </div>
    );
  }

  if (!data) {
    return (
      <div className="flex items-center gap-2 text-sm" style={{ color: "var(--muted)" }}>
        <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
        </svg>
        Loading batches…
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <p className="text-sm py-6 text-center" style={{ color: "var(--muted)" }}>
        No batches yet. Generate a sample batch above to get started.
      </p>
    );
  }

  // expose mutate for the generate button's onSuccess
  void mutate;

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="text-left" style={{ borderBottom: "1px solid var(--border)", color: "var(--muted)" }}>
            <th className="py-2 pr-4 font-medium">File Name</th>
            <th className="py-2 pr-4 font-medium">Status</th>
            <th className="py-2 pr-4 font-medium">Progress</th>
            <th className="py-2 pr-4 font-medium text-green-400">✓</th>
            <th className="py-2 pr-4 font-medium text-red-400">✗</th>
            <th className="py-2 pr-4 font-medium">Received At</th>
            <th className="py-2 font-medium"></th>
          </tr>
        </thead>
        <tbody>
          {data.map((batch) => {
            const pct = batch.totalRecords
              ? Math.round((batch.processedRecords / batch.totalRecords) * 100)
              : Math.round(batch.percentComplete ?? 0);
            return (
              <tr
                key={batch.batchId}
                className="border-t"
                style={{ borderColor: "var(--border)" }}
              >
                <td className="py-3 pr-4 font-mono text-xs max-w-[180px] truncate" title={batch.fileName}>
                  {batch.fileName}
                </td>
                <td className="py-3 pr-4">
                  <StatusBadge status={batch.status} />
                </td>
                <td className="py-3 pr-4 min-w-[160px]">
                  {batch.status === "Processing" && batch.totalRecords ? (
                    <div className="space-y-1">
                      <div className="w-full bg-gray-800 rounded-full h-2">
                        <div
                          className="bg-blue-500 h-2 rounded-full transition-all duration-500"
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                      <p className="text-xs" style={{ color: "var(--muted)" }}>
                        {batch.processedRecords} / {batch.totalRecords} ({pct}%)
                      </p>
                    </div>
                  ) : (
                    <span style={{ color: "var(--muted)" }}>—</span>
                  )}
                </td>
                <td className="py-3 pr-4 text-green-400 font-mono">{batch.succeededRecords}</td>
                <td className="py-3 pr-4 text-red-400 font-mono">{batch.failedRecords}</td>
                <td className="py-3 pr-4 text-xs" style={{ color: "var(--muted)" }}>
                  {formatDateTime(batch.receivedAt)}
                </td>
                <td className="py-3">
                  <Link
                    href={`/batches/${batch.batchId}`}
                    className="text-xs font-medium hover:text-white transition-colors"
                    style={{ color: "var(--accent-light)" }}
                  >
                    View Details →
                  </Link>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function BatchesPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  void refreshKey;

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <div className="flex items-center gap-3 mb-1">
          <Link
            href="/"
            className="text-sm hover:text-white transition-colors"
            style={{ color: "var(--muted)" }}
          >
            ← UC1 Policy Issuance
          </Link>
        </div>
        <h1 className="text-3xl font-bold">UC3 · Automated Renewal Batch</h1>
        <p className="text-sm mt-1" style={{ color: "var(--muted)" }}>
          Upload and process automated renewal batch files through the file processing pipeline.
        </p>
      </div>

      {/* UC3 Use Case Summary */}
      <div className="rounded-lg border p-6 space-y-3" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <h2 className="text-base font-semibold">Use Case Overview</h2>
        <p className="text-sm leading-relaxed" style={{ color: "var(--muted)" }}>
          A CSV renewal file dropped in the inbound zone is automatically detected and parsed.
          Each record in the file triggers an independent policy issuance — running the full compliance
          screening, customer identity lookup, PAS integration, and billing association flow.
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-1">
          <div className="rounded border p-3 text-sm" style={{ borderColor: "var(--border)" }}>
            <div className="font-semibold mb-1" style={{ color: "var(--accent-light)" }}>File-Triggered</div>
            <div style={{ color: "var(--muted)" }}>CSV files named <span className="font-mono">RENEWAL_*.csv</span> are polled from the inbound drop-zone every 5 seconds.</div>
          </div>
          <div className="rounded border p-3 text-sm" style={{ borderColor: "var(--border)" }}>
            <div className="font-semibold mb-1" style={{ color: "var(--accent-light)" }}>Per-Record Isolation</div>
            <div style={{ color: "var(--muted)" }}>Each record runs as an independent EDA saga. A failure in one record does not affect the others — partial batches complete successfully.</div>
          </div>
          <div className="rounded border p-3 text-sm" style={{ borderColor: "var(--border)" }}>
            <div className="font-semibold mb-1" style={{ color: "var(--accent-light)" }}>Observable End-to-End</div>
            <div style={{ color: "var(--muted)" }}>Click any processed record to view its full EDA sequence diagram — from file arrival through policy issuance and billing.</div>
          </div>
        </div>
      </div>

      {/* Generate section */}
      <GenerateSection onSuccess={() => setRefreshKey((k) => k + 1)} />

      {/* Batch list */}
      <div className="rounded-lg border p-6 space-y-4" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <h2 className="text-lg font-semibold">Active &amp; Recent Batches</h2>
        <BatchList />
      </div>
    </div>
  );
}
