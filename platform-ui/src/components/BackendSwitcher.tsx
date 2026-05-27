"use client";

import { useEffect, useState } from "react";

type Backend = "java" | "dotnet";

export function BackendSwitcher() {
  const [backend, setBackend] = useState<Backend | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetch("/api/backend")
      .then((r) => r.json())
      .then((d) => setBackend(d.backend as Backend));
  }, []);

  async function toggle() {
    const next: Backend = backend === "dotnet" ? "java" : "dotnet";
    setLoading(true);
    try {
      const response = await fetch("/api/backend", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ backend: next }),
      });
      if (!response.ok) {
        throw new Error(`Failed to switch backend: ${response.status}`);
      }
      setBackend(next);
      window.dispatchEvent(new CustomEvent("backend-changed", { detail: { backend: next } }));
    } finally {
      setLoading(false);
    }
  }

  if (!backend) return null;

  const isDotnet = backend === "dotnet";

  return (
    <button
      onClick={toggle}
      disabled={loading}
      title={`Active: ${isDotnet ? ".NET Stack" : "Java Stack"} — click to switch`}
      className="flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-semibold transition-all disabled:opacity-50"
      style={{
        borderColor: isDotnet ? "#a855f7" : "#06b6d4",
        background: isDotnet ? "#a855f722" : "#06b6d422",
        color: isDotnet ? "#c084fc" : "#22d3ee",
        cursor: loading ? "wait" : "pointer",
      }}
    >
      <span>{isDotnet ? "⬡ .NET" : "☕ Java"}</span>
      <span style={{ opacity: 0.5 }}>↔</span>
    </button>
  );
}
