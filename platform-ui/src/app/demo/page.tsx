"use client";

import { useState, useCallback } from "react";
import Link from "next/link";

// ─── Types ────────────────────────────────────────────────────────────────────

interface ServiceHealth {
  name: string;
  url: string;
  stack: "java" | "dotnet";
  status: "UP" | "DOWN";
  latencyMs: number;
  error?: string;
}

interface HealthResult {
  checkedAt: string;
  totalServices: number;
  healthyServices: number;
  allHealthy: boolean;
  services: ServiceHealth[];
}

interface ClearDetail {
  db: string;
  collection: string;
  deletedCount: number;
  success: boolean;
  error?: string;
}

interface ClearResult {
  clearedAt: string;
  collectionsCleared: number;
  details: ClearDetail[];
  success: boolean;
}

interface SeedDetail {
  correlationId: string;
  policyNumber?: string;
  appraisalId?: string;
  statusCode?: number;
  status?: string;
  success: boolean;
  error?: string;
}

interface SeedResult {
  seededAt: string;
  scenariosInserted: number;
  details: SeedDetail[];
  success: boolean;
}

interface ResetResult {
  resetAt: string;
  success: boolean;
  readyForDemo: boolean;
  summary: string;
  health: HealthResult;
  clear: ClearResult;
  seed: SeedResult;
}

type Phase = "idle" | "loading" | "done" | "error";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const statusColor = (s: "UP" | "DOWN") =>
  s === "UP" ? "var(--success)" : "var(--danger)";

const stackBadge = (stack: "java" | "dotnet") =>
  stack === "java"
    ? { label: "Java", bg: "#1e3a5f", color: "#60a5fa" }
    : { label: ".NET", bg: "#3b1f5e", color: "#c084fc" };

// ─── Demo Page ────────────────────────────────────────────────────────────────

export default function DemoPage() {
  const [phase, setPhase] = useState<Phase>("idle");
  const [healthResult, setHealthResult] = useState<HealthResult | null>(null);
  const [resetResult, setResetResult] = useState<ResetResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<"health" | "clear" | "seed">("health");

  const checkHealth = useCallback(async () => {
    setPhase("loading");
    setErrorMsg(null);
    try {
      const res = await fetch("/api/demo/health");
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: HealthResult = await res.json();
      setHealthResult(data);
      setPhase("done");
    } catch (e) {
      setErrorMsg(e instanceof Error ? e.message : String(e));
      setPhase("error");
    }
  }, []);

  const fullReset = useCallback(async () => {
    setPhase("loading");
    setErrorMsg(null);
    setResetResult(null);
    try {
      const res = await fetch("/api/demo/reset", { method: "POST" });
      const data: ResetResult = await res.json();
      setResetResult(data);
      setHealthResult(data.health);
      setActiveTab("health");
      setPhase("done");
    } catch (e) {
      setErrorMsg(e instanceof Error ? e.message : String(e));
      setPhase("error");
    }
  }, []);

  const isLoading = phase === "loading";

  return (
    <div style={{ maxWidth: 900 }}>
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold mb-1" style={{ color: "var(--accent-light)" }}>
          🎬 Demo Reset
        </h1>
        <p style={{ color: "var(--muted)" }} className="text-sm">
          One-click demo prep — no scripts, no manual steps.
        </p>
      </div>

      {/* Action buttons */}
      <div className="flex gap-3 mb-6 flex-wrap">
        <button
          onClick={fullReset}
          disabled={isLoading}
          className="px-5 py-2.5 rounded-lg font-semibold text-sm transition-opacity disabled:opacity-50"
          style={{ background: "var(--accent)", color: "#fff" }}
        >
          {isLoading ? "⏳ Resetting…" : "🔄 Full Demo Reset"}
        </button>
        <button
          onClick={checkHealth}
          disabled={isLoading}
          className="px-4 py-2.5 rounded-lg font-semibold text-sm transition-opacity disabled:opacity-50"
          style={{ background: "var(--surface)", border: "1px solid var(--border)", color: "var(--text)" }}
        >
          {isLoading ? "⏳ Checking…" : "❤️ Check Health Only"}
        </button>
      </div>

      {/* Reset summary banner */}
      {resetResult && (
        <div
          className="rounded-lg px-4 py-3 mb-5 text-sm font-medium"
          style={{
            background: resetResult.success ? "#14532d" : "#7f1d1d",
            border: `1px solid ${resetResult.success ? "var(--success)" : "var(--danger)"}`,
            color: resetResult.success ? "var(--success)" : "#fca5a5",
          }}
        >
          {resetResult.summary}
        </div>
      )}

      {/* Error banner */}
      {phase === "error" && errorMsg && (
        <div
          className="rounded-lg px-4 py-3 mb-5 text-sm"
          style={{ background: "#7f1d1d", border: "1px solid var(--danger)", color: "#fca5a5" }}
        >
          ❌ {errorMsg} — is platform-integration-service running?
        </div>
      )}

      {/* Results tabs */}
      {(healthResult || resetResult) && (
        <div
          className="rounded-xl overflow-hidden"
          style={{ border: "1px solid var(--border)", background: "var(--surface)" }}
        >
          {/* Tab bar */}
          <div className="flex" style={{ borderBottom: "1px solid var(--border)" }}>
            {(["health", "clear", "seed"] as const).map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className="px-5 py-3 text-sm font-medium transition-colors capitalize"
                style={{
                  background: activeTab === tab ? "var(--bg)" : "transparent",
                  color: activeTab === tab ? "var(--accent-light)" : "var(--muted)",
                  borderBottom: activeTab === tab ? "2px solid var(--accent)" : "none",
                }}
                disabled={tab !== "health" && !resetResult}
              >
                {tab === "health"
                  ? `Health ${healthResult ? `(${healthResult.healthyServices}/${healthResult.totalServices})` : ""}`
                  : tab === "clear"
                  ? `Clear ${resetResult ? `(${resetResult.clear.collectionsCleared})` : ""}`
                  : `Seed ${resetResult ? `(${resetResult.seed.scenariosInserted})` : ""}`}
              </button>
            ))}
          </div>

          {/* Health tab */}
          {activeTab === "health" && healthResult && (
            <div className="p-4">
              <p className="text-xs mb-3" style={{ color: "var(--muted)" }}>
                Checked at {new Date(healthResult.checkedAt).toLocaleTimeString()} ·{" "}
                {healthResult.healthyServices}/{healthResult.totalServices} services healthy
              </p>
              <div className="space-y-1.5">
                {healthResult.services.map((svc) => {
                  const badge = stackBadge(svc.stack);
                  return (
                    <div
                      key={svc.name}
                      className="flex items-center justify-between rounded px-3 py-2 text-sm"
                      style={{ background: "var(--bg)" }}
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <span
                          className="w-2 h-2 rounded-full flex-shrink-0"
                          style={{ background: statusColor(svc.status) }}
                        />
                        <span
                          className="text-xs px-1.5 py-0.5 rounded flex-shrink-0"
                          style={{ background: badge.bg, color: badge.color }}
                        >
                          {badge.label}
                        </span>
                        <span className="truncate" style={{ color: "var(--text)" }}>
                          {svc.name}
                        </span>
                      </div>
                      <div className="flex items-center gap-3 flex-shrink-0 ml-3">
                        <span
                          className="text-xs font-semibold"
                          style={{ color: statusColor(svc.status) }}
                        >
                          {svc.status}
                        </span>
                        <span className="text-xs" style={{ color: "var(--muted)" }}>
                          {svc.latencyMs}ms
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Clear tab */}
          {activeTab === "clear" && resetResult?.clear && (
            <div className="p-4">
              <p className="text-xs mb-3" style={{ color: "var(--muted)" }}>
                Cleared at {new Date(resetResult.clear.clearedAt).toLocaleTimeString()} ·{" "}
                {resetResult.clear.collectionsCleared} collections cleared
              </p>
              <div className="space-y-1.5">
                {resetResult.clear.details.map((d) => (
                  <div
                    key={`${d.db}.${d.collection}`}
                    className="flex items-center justify-between rounded px-3 py-2 text-sm"
                    style={{ background: "var(--bg)" }}
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{ background: d.success ? "var(--success)" : "var(--danger)" }}
                      />
                      <span style={{ color: "var(--muted)" }}>{d.db}</span>
                      <span>·</span>
                      <span>{d.collection}</span>
                    </div>
                    <span className="text-xs" style={{ color: "var(--muted)" }}>
                      {d.deletedCount} deleted
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Seed tab */}
          {activeTab === "seed" && resetResult?.seed && (
            <div className="p-4">
              <p className="text-xs mb-3" style={{ color: "var(--muted)" }}>
                Seeded at {new Date(resetResult.seed.seededAt).toLocaleTimeString()} ·{" "}
                {resetResult.seed.scenariosInserted} scenarios ready
              </p>
              <div className="space-y-1.5">
                {resetResult.seed.details.map((d) => (
                  <div
                    key={d.correlationId}
                    className="flex items-center justify-between rounded px-3 py-2 text-sm"
                    style={{ background: "var(--bg)" }}
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{ background: d.success ? "var(--success)" : "var(--danger)" }}
                      />
                      <span style={{ color: "var(--muted)" }}>{d.policyNumber}</span>
                      <span>·</span>
                      <span>{d.appraisalId}</span>
                      <span className="text-xs px-1.5 py-0.5 rounded" style={{ background: "var(--surface)", color: "var(--muted)" }}>
                        SC {d.statusCode}
                      </span>
                    </div>
                    <span
                      className="text-xs font-semibold"
                      style={{
                        color:
                          d.status === "Completed" ? "var(--success)" :
                          d.status === "TimedOut"  ? "var(--warning)" : "var(--text)",
                      }}
                    >
                      {d.status}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Scenario reference */}
      <div
        className="rounded-xl p-4 mt-6"
        style={{ border: "1px solid var(--border)", background: "var(--surface)" }}
      >
        <h2 className="text-sm font-semibold mb-3" style={{ color: "var(--accent-light)" }}>
          📋 Demo Scenarios (seeded on reset)
        </h2>
        <table className="w-full text-xs" style={{ color: "var(--text)" }}>
          <thead>
            <tr style={{ color: "var(--muted)" }}>
              <th className="text-left pb-2">Policy</th>
              <th className="text-left pb-2">Inspection</th>
              <th className="text-left pb-2">SC</th>
              <th className="text-left pb-2">Type</th>
              <th className="text-left pb-2">UW</th>
              <th className="text-left pb-2">Scenario</th>
            </tr>
          </thead>
          <tbody className="space-y-1">
            {[
              { pol: "POL-12345", ins: "INS-001", sc: 6,  type: "A", uw: "UA",  desc: "Happy path — UA appraisal, 45-day suspense" },
              { pol: "POL-12346", ins: "INS-002", sc: 6,  type: "B", uw: "UST", desc: "Happy path — UST appraisal, 14-day suspense" },
              { pol: "POL-12347", ins: "INS-003", sc: 6,  type: "I", uw: "UA",  desc: "Inspection — UA, no Masterpiece TX90" },
              { pol: "POL-12348", ins: "INS-004", sc: 6,  type: "A", uw: "UA",  desc: "⏱ Timeout scenario — saga exceeds watchdog" },
              { pol: "POL-12349", ins: "INS-005", sc: 15, type: "A", uw: "UA",  desc: "SC=15 Completed — Masterpiece TX90 fired" },
            ].map((row) => (
              <tr key={row.ins}>
                <td className="py-1" style={{ color: "var(--accent-light)" }}>{row.pol}</td>
                <td className="py-1">{row.ins}</td>
                <td className="py-1">{row.sc}</td>
                <td className="py-1">{row.type}</td>
                <td className="py-1">{row.uw}</td>
                <td className="py-1" style={{ color: "var(--muted)" }}>{row.desc}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Quick links */}
      <div className="mt-4 flex gap-3 flex-wrap text-xs" style={{ color: "var(--muted)" }}>
        <Link href="/uc4" style={{ color: "var(--accent-light)" }}>→ UC4 Appraisals</Link>
        <Link href="/ops" style={{ color: "var(--accent-light)" }}>→ Operations</Link>
        <Link href="/events" style={{ color: "var(--accent-light)" }}>→ Live Events</Link>
      </div>
    </div>
  );
}
