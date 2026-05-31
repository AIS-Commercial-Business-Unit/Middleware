"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

export default function OpsLandingPage() {
  const router = useRouter();
  const [value, setValue] = useState("");

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const id = value.trim();
    if (id) router.push(`/ops/${id}`);
  }

  return (
    <div className="space-y-8">
      <div>
        <p className="text-xs font-mono mb-1" style={{ color: "var(--muted)" }}>
          Operations &amp; Observability
        </p>
        <h1 className="text-2xl font-bold mb-2">Flow Tracer</h1>
        <p className="text-sm" style={{ color: "var(--muted)" }}>
          Paste a <code className="font-mono px-1 rounded" style={{ background: "var(--border)" }}>correlation ID (issuanceId or requestId)</code> to
          watch any use case (UC1 policy issuance, UC4 appraisal documents, or any future flow) in real time — inter-service Kafka messages, saga state, and live log tail.
        </p>
      </div>

      <form
        onSubmit={handleSearch}
        className="rounded-lg border p-6 space-y-4"
        style={{ borderColor: "var(--border)", background: "var(--surface)" }}
      >
        <div className="flex gap-3">
          <input
            value={value}
            onChange={(e) => setValue(e.target.value)}
            placeholder="e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6"
            className="flex-1 rounded px-3 py-2 text-sm font-mono"
            style={{ background: "var(--bg)", border: "1px solid var(--border)", color: "var(--text)" }}
          />
          <button
            type="submit"
            disabled={!value.trim()}
            className="px-5 py-2 rounded text-sm font-semibold disabled:opacity-50"
            style={{ background: "var(--accent)", color: "white" }}
          >
            Trace →
          </button>
        </div>
        <p className="text-xs" style={{ color: "var(--muted)" }}>
          Or{" "}
          <Link href="/" className="underline" style={{ color: "var(--accent-light)" }}>
            submit a new IssuePolicy command
          </Link>{" "}
          or{" "}
          <Link href="/uc4" className="underline" style={{ color: "var(--accent-light)" }}>
            open the UC4 appraisal demos
          </Link>{" "}
          — both flows can route you back here with the right correlation ID.
        </p>
      </form>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {[
          {
            title: "Sequence Diagram",
            desc: "Live SVG showing all inter-service messages in the flow, dynamically built from structured EDA_FLOW log entries.",
          },
          {
            title: "Saga State Panel",
            desc: "Real-time IssuanceSaga state machine view — current status, policy numbers, PAS routing, parallel branch completion.",
          },
          {
            title: "Log Tail (Loki)",
            desc: "Last 60 structured log entries from every service that processed this issuanceId, timestamped and color-coded by level.",
          },
        ].map((card) => (
          <div
            key={card.title}
            className="rounded-lg border p-4 space-y-1"
            style={{ borderColor: "var(--border)", background: "var(--surface)" }}
          >
            <p className="text-sm font-semibold" style={{ color: "var(--accent-light)" }}>
              {card.title}
            </p>
            <p className="text-xs" style={{ color: "var(--muted)" }}>
              {card.desc}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
