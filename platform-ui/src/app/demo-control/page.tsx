"use client";

import { useState, useEffect, useRef, useCallback } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────

type ServiceStatus = "healthy" | "unhealthy" | "checking";

interface ServiceHealth {
  name: string;
  group: "java" | "dotnet" | "infra";
  status: ServiceStatus;
  responseMs: number | null;
  detail: string | null;
}

interface HealthResponse {
  checkedAt: string;
  total: number;
  healthy: number;
  unhealthy: number;
  services: ServiceHealth[];
}

interface ResetStep {
  step: string;
  status: "ok" | "error" | "running" | "pending";
  message: string;
}

type ActionState = "idle" | "running" | "success" | "error";

interface LogEntry {
  id: string;
  ts: string;
  level: "info" | "success" | "error" | "warn";
  message: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function now() {
  return new Date().toLocaleTimeString("en-US", { hour12: false, hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function statusBadge(status: ServiceStatus) {
  switch (status) {
    case "healthy":  return { dot: "🟢", color: "#22c55e", label: "Healthy" };
    case "unhealthy": return { dot: "🔴", color: "#ef4444", label: "Unhealthy" };
    case "checking": return { dot: "🟡", color: "#f59e0b", label: "Checking…" };
  }
}


function stepIcon(status: ResetStep["status"]) {
  switch (status) {
    case "ok":      return "✅";
    case "error":   return "❌";
    case "running": return "⏳";
    case "pending": return "○";
  }
}

const RESET_STEP_LABELS: Record<string, string> = {
  health_check: "Health Check",
  clear_data:   "Clear Data",
  seed_data:    "Seed Sample Data",
  verify:       "Verify",
};

const FAKE_STEPS: ResetStep[] = [
  { step: "health_check", status: "pending", message: "Waiting…" },
  { step: "clear_data",   status: "pending", message: "Waiting…" },
  { step: "seed_data",    status: "pending", message: "Waiting…" },
  { step: "verify",       status: "pending", message: "Waiting…" },
];

// ─── Component ────────────────────────────────────────────────────────────────

export default function DemoControlPage() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthLoading, setHealthLoading] = useState(true);
  const [lastChecked, setLastChecked] = useState<string | null>(null);

  const [resetState, setResetState] = useState<ActionState>("idle");
  const [resetSteps, setResetSteps] = useState<ResetStep[]>(FAKE_STEPS);
  const [resetError, setResetError] = useState<string | null>(null);

  const [clearState, setClearState] = useState<ActionState>("idle");
  const [seedState, setSeedState] = useState<ActionState>("idle");

  const [log, setLog] = useState<LogEntry[]>([]);
  const logEndRef = useRef<HTMLDivElement>(null);

  const pushLog = useCallback((level: LogEntry["level"], message: string) => {
    setLog((prev) => [
      ...prev,
      { id: crypto.randomUUID(), ts: now(), level, message },
    ]);
  }, []);

  // ── Health polling ─────────────────────────────────────────────────────────
  const fetchHealth = useCallback(async (silent = false) => {
    if (!silent) setHealthLoading(true);
    try {
      const res = await fetch("/api/demo/health");
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: HealthResponse = await res.json();
      setHealth(data);
      setLastChecked(new Date(data.checkedAt).toLocaleTimeString());
      if (!silent) pushLog("success", `Health check complete — ${data.healthy}/${data.total} healthy`);
    } catch (err) {
      if (!silent) pushLog("error", `Health check failed: ${err instanceof Error ? err.message : "Unknown error"}`);
    } finally {
      setHealthLoading(false);
    }
  }, [pushLog]);

  useEffect(() => {
    fetchHealth();
    const timer = setInterval(() => fetchHealth(true), 10_000);
    return () => clearInterval(timer);
  }, [fetchHealth]);

  // scroll log to bottom on new entries
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [log]);

  // ── Reset ──────────────────────────────────────────────────────────────────
  async function handleReset() {
    if (resetState === "running") return;
    setResetState("running");
    setResetError(null);
    setResetSteps(FAKE_STEPS.map((s, i) => ({
      ...s,
      status: i === 0 ? "running" : "pending",
      message: i === 0 ? "Running…" : "Waiting…",
    })));
    pushLog("info", "🔄 Demo reset started…");

    // Animate steps while API call is in flight
    const stepDurations = [600, 500, 700, 400];
    let stepIdx = 0;
    const advanceStep = () => {
      if (stepIdx >= FAKE_STEPS.length) return;
      setResetSteps((prev) =>
        prev.map((s, i) => {
          if (i < stepIdx)    return { ...s, status: "ok", message: "Done" };
          if (i === stepIdx)  return { ...s, status: "running", message: "Running…" };
          return s;
        })
      );
    };
    const timers: ReturnType<typeof setTimeout>[] = [];
    let elapsed = 0;
    for (const dur of stepDurations) {
      const capture = stepIdx;
      timers.push(setTimeout(() => {
        stepIdx = capture;
        advanceStep();
        stepIdx++;
      }, elapsed));
      elapsed += dur;
    }

    try {
      const res = await fetch("/api/demo/reset", { method: "POST" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      timers.forEach(clearTimeout);
      const finalSteps: ResetStep[] = data.steps ?? FAKE_STEPS.map((s) => ({ ...s, status: "ok", message: "Done" }));
      setResetSteps(finalSteps.map((s) => ({ ...s, status: s.status === "ok" ? "ok" : "error" })));
      setResetState("success");
      if (data.isMockData) {
        pushLog("warn", `✅ Reset complete (mock data — DEMO_API_URL not yet live). Steps: ${finalSteps.length}`);
      } else {
        pushLog("success", `✅ Reset complete in ${data.durationMs ?? "?"}ms`);
      }
      fetchHealth(true);
    } catch (err) {
      timers.forEach(clearTimeout);
      const msg = err instanceof Error ? err.message : "Unknown error";
      setResetError(msg);
      setResetState("error");
      setResetSteps(FAKE_STEPS.map((s) => ({ ...s, status: "pending", message: "–" })));
      pushLog("error", `❌ Reset failed: ${msg}`);
    }
  }

  // ── Clear ──────────────────────────────────────────────────────────────────
  async function handleClear() {
    if (clearState === "running") return;
    setClearState("running");
    pushLog("info", "🗑️  Clearing UC4 data…");
    try {
      const res = await fetch("/api/demo/clear", { method: "POST" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setClearState("success");
      pushLog("success", `✅ Data cleared${data.isMockData ? " (mock)" : ""}`);
      setTimeout(() => setClearState("idle"), 3000);
    } catch (err) {
      setClearState("error");
      pushLog("error", `❌ Clear failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      setTimeout(() => setClearState("idle"), 4000);
    }
  }

  // ── Seed ───────────────────────────────────────────────────────────────────
  async function handleSeed() {
    if (seedState === "running") return;
    setSeedState("running");
    pushLog("info", "🌱 Seeding sample appraisals…");
    try {
      const res = await fetch("/api/demo/seed", { method: "POST" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setSeedState("success");
      pushLog("success", `✅ Seeded ${data.seeded ?? "?"} appraisals${data.isMockData ? " (mock)" : ""}`);
      setTimeout(() => setSeedState("idle"), 3000);
    } catch (err) {
      setSeedState("error");
      pushLog("error", `❌ Seed failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      setTimeout(() => setSeedState("idle"), 4000);
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────
  const javaServices  = health?.services.filter((s) => s.group === "java")   ?? [];
  const dotnetServices = health?.services.filter((s) => s.group === "dotnet") ?? [];
  const infraServices = health?.services.filter((s) => s.group === "infra")  ?? [];

  const allHealthy = health ? health.unhealthy === 0 : null;

  return (
    <div className="space-y-8">
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-1">🎛️ Demo Control Panel</h1>
          <p className="text-sm" style={{ color: "var(--muted)" }}>
            Reset, seed, and verify the demo environment. Auto-refreshing health every 10 s.
          </p>
        </div>
        <div className="text-right">
          {allHealthy === true  && <div className="text-sm font-semibold" style={{ color: "var(--success)" }}>🟢 All Systems Go</div>}
          {allHealthy === false && <div className="text-sm font-semibold" style={{ color: "var(--danger)" }}>🔴 Service Issues Detected</div>}
          {allHealthy === null  && <div className="text-sm" style={{ color: "var(--muted)" }}>Checking…</div>}
          {lastChecked && <div className="text-xs mt-1" style={{ color: "var(--muted)" }}>Last checked {lastChecked}</div>}
        </div>
      </div>

      {/* ── Main grid ───────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

        {/* ── Left column: Health Status ────────────────────────────────────── */}
        <div className="lg:col-span-2 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Service Health</h2>
            <button
              onClick={() => fetchHealth()}
              disabled={healthLoading}
              className="text-xs px-3 py-1 rounded border transition-colors disabled:opacity-50"
              style={{ borderColor: "var(--border)", background: "var(--surface)", color: "var(--accent-light)" }}
            >
              {healthLoading ? "Checking…" : "↻ Refresh"}
            </button>
          </div>

          {/* Health summary strip */}
          {health && (
            <div className="rounded-lg border px-4 py-3 flex items-center gap-6 text-sm"
              style={{ borderColor: allHealthy ? "#22c55e44" : "#ef444444", background: "var(--surface)" }}>
              <span style={{ color: "var(--success)" }}><strong>{health.healthy}</strong> healthy</span>
              <span style={{ color: "var(--danger)" }}><strong>{health.unhealthy}</strong> unhealthy</span>
              <span style={{ color: "var(--muted)" }}>{health.total} total</span>
            </div>
          )}

          {/* Service groups */}
          {(
            [
              { key: "java",   label: "☕ Java Stack",      services: javaServices },
              { key: "dotnet", label: "🔷 .NET Stack",      services: dotnetServices },
              { key: "infra",  label: "🏗️ Infrastructure",  services: infraServices },
            ] as const
          ).map(({ key, label, services }) => (
            <div key={key} className="rounded-lg border overflow-hidden"
              style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
              <div className="px-4 py-2 text-xs font-semibold uppercase tracking-wider"
                style={{ background: "var(--bg)", color: "var(--muted)", borderBottom: "1px solid var(--border)" }}>
                {label}
              </div>
              <div className="divide-y" style={{ borderColor: "var(--border)" }}>
                {services.length === 0 ? (
                  <div className="px-4 py-3 text-sm" style={{ color: "var(--muted)" }}>
                    {healthLoading ? "Checking…" : "No data"}
                  </div>
                ) : (
                  services.map((svc) => {
                    const badge = statusBadge(svc.status);
                    return (
                      <div key={`${svc.group}-${svc.name}`}
                        className="px-4 py-2.5 flex items-center justify-between text-sm">
                        <div className="flex items-center gap-2.5">
                          <span>{badge.dot}</span>
                          <span className="font-medium">{svc.name}</span>
                          {svc.detail && svc.detail !== "UP" && (
                            <span className="text-xs font-mono px-1.5 py-0.5 rounded"
                              style={{ background: "var(--bg)", color: "var(--muted)" }}>
                              {svc.detail}
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-3">
                          {svc.status === "unhealthy" && (
                            <span className="text-xs" style={{ color: "var(--danger)" }}>UNHEALTHY</span>
                          )}
                          {svc.responseMs !== null && (
                            <span className="text-xs font-mono tabular-nums" style={{ color: "var(--muted)" }}>
                              {svc.responseMs} ms
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          ))}
        </div>

        {/* ── Right column: Reset + Actions ─────────────────────────────────── */}
        <div className="space-y-4">

          {/* Demo Reset card */}
          <div className="rounded-lg border p-5 space-y-4"
            style={{ borderColor: resetState === "error" ? "var(--danger)" : resetState === "success" ? "var(--success)" : "var(--border)", background: "var(--surface)" }}>
            <div>
              <h2 className="text-lg font-semibold mb-0.5">Demo Reset</h2>
              <p className="text-xs" style={{ color: "var(--muted)" }}>
                Health check → clear data → seed → verify
              </p>
            </div>

            <button
              onClick={handleReset}
              disabled={resetState === "running"}
              className="w-full rounded-lg py-3.5 font-bold text-base transition-all disabled:opacity-60 disabled:cursor-not-allowed"
              style={{
                background: resetState === "error"
                  ? "var(--danger)"
                  : resetState === "success"
                  ? "var(--success)"
                  : "var(--accent)",
                color: "white",
              }}
            >
              {resetState === "running" ? "⏳ Resetting…" :
               resetState === "success" ? "✅ Reset Complete" :
               resetState === "error"   ? "❌ Reset Failed — Retry" :
               "🔄 Reset Demo"}
            </button>

            {/* Progress steps */}
            {resetState !== "idle" && (
              <div className="space-y-1.5">
                {resetSteps.map((step) => (
                  <div key={step.step} className="flex items-center gap-2 text-sm">
                    <span className="text-base leading-none">{stepIcon(step.status)}</span>
                    <span className="font-medium w-32 shrink-0">
                      {RESET_STEP_LABELS[step.step] ?? step.step}
                    </span>
                    <span className="text-xs truncate" style={{ color: "var(--muted)" }}>
                      {step.message}
                    </span>
                  </div>
                ))}
              </div>
            )}

            {resetError && (
              <div className="text-xs rounded px-3 py-2"
                style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>
                {resetError}
              </div>
            )}

            {resetState === "success" && (
              <button
                onClick={() => { setResetState("idle"); setResetSteps(FAKE_STEPS); }}
                className="w-full text-xs py-1.5 rounded border transition-colors"
                style={{ borderColor: "var(--border)", color: "var(--muted)" }}
              >
                Reset panel
              </button>
            )}
          </div>

          {/* Quick Actions card */}
          <div className="rounded-lg border p-5 space-y-3"
            style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
            <h2 className="text-base font-semibold">Quick Actions</h2>

            <QuickActionButton
              label="🗑️ Clear UC4 Data"
              description="Remove all appraisal sagas from MongoDB"
              state={clearState}
              onClick={handleClear}
              activeColor="var(--warning)"
            />

            <QuickActionButton
              label="🌱 Seed Sample Appraisals"
              description="Insert 3 demo appraisal sagas"
              state={seedState}
              onClick={handleSeed}
              activeColor="var(--success)"
            />

            <QuickActionButton
              label="❤️ Run Health Check"
              description="Poll all service health endpoints"
              state={healthLoading ? "running" : "idle"}
              onClick={() => fetchHealth()}
              activeColor="var(--accent)"
            />
          </div>

          {/* Demo schedule reminder */}
          <div className="rounded-lg border p-4 text-xs space-y-1.5"
            style={{ borderColor: "#f59e0b44", background: "#f59e0b0d" }}>
            <p className="font-semibold" style={{ color: "#f59e0b" }}>📅 Demo Schedule</p>
            <p style={{ color: "var(--muted)" }}>Prep session — Fri May 30, 11:30 AM</p>
            <p style={{ color: "var(--muted)" }}>Final demo — Mon Jun 2</p>
          </div>
        </div>
      </div>

      {/* ── Status Log ────────────────────────────────────────────────────────── */}
      <div className="rounded-lg border overflow-hidden"
        style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <div className="px-4 py-2.5 flex items-center justify-between"
          style={{ borderBottom: "1px solid var(--border)", background: "var(--bg)" }}>
          <h2 className="text-sm font-semibold">Status Log</h2>
          {log.length > 0 && (
            <button
              onClick={() => setLog([])}
              className="text-xs"
              style={{ color: "var(--muted)" }}
            >
              Clear
            </button>
          )}
        </div>
        <div className="h-48 overflow-y-auto font-mono text-xs p-3 space-y-1">
          {log.length === 0 ? (
            <div style={{ color: "var(--muted)" }}>No actions yet. Run a reset or health check to begin.</div>
          ) : (
            log.map((entry) => (
              <div key={entry.id} className="flex items-start gap-2">
                <span className="shrink-0 tabular-nums" style={{ color: "var(--muted)" }}>
                  {entry.ts}
                </span>
                <span
                  className="shrink-0"
                  style={{
                    color: entry.level === "success" ? "var(--success)"
                         : entry.level === "error"   ? "var(--danger)"
                         : entry.level === "warn"    ? "var(--warning)"
                         : "var(--accent-light)",
                  }}
                >
                  {entry.level === "success" ? "✓" :
                   entry.level === "error"   ? "✗" :
                   entry.level === "warn"    ? "⚠" : "·"}
                </span>
                <span style={{ color: entry.level === "error" ? "var(--danger)" : "var(--text)" }}>
                  {entry.message}
                </span>
              </div>
            ))
          )}
          <div ref={logEndRef} />
        </div>
      </div>
    </div>
  );
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function QuickActionButton({
  label,
  description,
  state,
  onClick,
  activeColor,
}: {
  label: string;
  description: string;
  state: ActionState;
  onClick: () => void;
  activeColor: string;
}) {
  const isRunning = state === "running";
  const isSuccess = state === "success";
  const isError   = state === "error";

  return (
    <button
      onClick={onClick}
      disabled={isRunning}
      className="w-full text-left rounded-lg border px-3 py-2.5 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
      style={{
        borderColor: isSuccess ? "var(--success)" : isError ? "var(--danger)" : "var(--border)",
        background: "var(--bg)",
      }}
    >
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium" style={{ color: isSuccess ? "var(--success)" : isError ? "var(--danger)" : "var(--text)" }}>
          {isRunning ? "⏳ Running…" : isSuccess ? `✅ ${label.slice(2)}` : isError ? `❌ Failed` : label}
        </span>
        {!isRunning && !isSuccess && !isError && (
          <span className="text-xs" style={{ color: activeColor }}>→</span>
        )}
      </div>
      <p className="text-xs mt-0.5" style={{ color: "var(--muted)" }}>{description}</p>
    </button>
  );
}
