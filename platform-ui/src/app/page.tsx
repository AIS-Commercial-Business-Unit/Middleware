"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import clsx from "clsx";

const POLICY_TYPES = [
  { code: 1, subCode: 0, label: "Commercial Property (DuckCreek Commercial)", pas: "DuckCreek-Commercial" },
  { code: 2, subCode: 0, label: "Commercial Liability (DuckCreek Commercial)", pas: "DuckCreek-Commercial" },
  { code: 42, subCode: 0, label: "Commercial Auto (DuckCreek Commercial)", pas: "DuckCreek-Commercial" },
  { code: 5, subCode: 0, label: "Personal Auto (DuckCreek Personal)", pas: "DuckCreek-Personal" },
  { code: 6, subCode: 0, label: "Homeowners (DuckCreek Personal)", pas: "DuckCreek-Personal" },
  { code: 10, subCode: 0, label: "Directors & Officers (ForeFront)", pas: "ForeFront" },
  { code: 12, subCode: 0, label: "Employment Practices (ForeFront)", pas: "ForeFront" },
];

const CHANNELS = ["DirectRequest", "AutomatedRenewal", "LegacyQueue"];

export default function HomePage() {
  const router = useRouter();
  const [accountId, setAccountId] = useState("ACC-" + Math.random().toString(36).slice(2, 10).toUpperCase());
  const [selectedType, setSelectedType] = useState(POLICY_TYPES[0]);
  const [channel, setChannel] = useState("DirectRequest");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    const payload = {
      issuanceId: crypto.randomUUID(),
      accountId,
      submittingChannel: channel,
      requestedAt: new Date().toISOString(),
      policies: [{ policyTypeCode: selectedType.code, policyTypeSubCode: selectedType.subCode, policyData: {} }],
    };
    try {
      const res = await fetch("/api/policies/issue", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      router.push(`/saga/${data.issuanceId}`);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Unknown error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold mb-1">UC1 · Policy Issuance</h1>
        <p className="text-sm" style={{ color: "var(--muted)" }}>
          Submit an IssuePolicy command. The saga runs asynchronously — you will be redirected to the Saga Explorer.
        </p>
      </div>
      <div className="rounded-lg border p-4 text-sm space-y-1" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <p className="font-semibold" style={{ color: "var(--accent-light)" }}>What happens after you submit</p>
        <ol className="list-decimal list-inside space-y-1" style={{ color: "var(--muted)" }}>
          <li>API returns a correlation ID immediately (async saga)</li>
          <li>Compliance check via RSK3X3 stub</li>
          <li>Account record retrieved from ERM7X1 stub</li>
          <li>Routed to PAS by policyTypeCode (Content-Based Router)</li>
          <li>Billing + Customer update run in parallel</li>
          <li>Saga join triggers PolicyIssued event</li>
        </ol>
      </div>
      <form onSubmit={submit} className="rounded-lg border p-6 space-y-5" style={{ borderColor: "var(--border)", background: "var(--surface)" }}>
        <div className="space-y-2">
          <label className="block text-sm font-medium">Account ID</label>
          <input value={accountId} onChange={(e) => setAccountId(e.target.value)}
            className="w-full rounded px-3 py-2 text-sm font-mono"
            style={{ background: "var(--bg)", border: "1px solid var(--border)", color: "var(--text)" }} required />
        </div>
        <div className="space-y-2">
          <label className="block text-sm font-medium">Policy Type</label>
          <select value={selectedType.code} onChange={(e) => setSelectedType(POLICY_TYPES.find((t) => t.code === +e.target.value)!)}
            className="w-full rounded px-3 py-2 text-sm"
            style={{ background: "var(--bg)", border: "1px solid var(--border)", color: "var(--text)" }}>
            {POLICY_TYPES.map((t) => (<option key={t.code} value={t.code}>{t.label}</option>))}
          </select>
          <p className="text-xs" style={{ color: "var(--muted)" }}>
            Routes to PAS: <strong>{selectedType.pas}</strong> via policyTypeCode={selectedType.code}
          </p>
        </div>
        <div className="space-y-2">
          <label className="block text-sm font-medium">Submitting Channel</label>
          <div className="flex gap-3">
            {CHANNELS.map((c) => (
              <button key={c} type="button" onClick={() => setChannel(c)}
                className={clsx("px-3 py-1.5 rounded text-sm border transition-colors")}
                style={{ borderColor: channel === c ? "var(--accent)" : "var(--border)", background: channel === c ? "var(--accent)" : "transparent", color: channel === c ? "white" : "var(--muted)" }}>
                {c}
              </button>
            ))}
          </div>
        </div>
        {error && <div className="rounded px-3 py-2 text-sm" style={{ background: "#2d1515", color: "var(--danger)", border: "1px solid var(--danger)" }}>{error}</div>}
        <button type="submit" disabled={loading}
          className="w-full rounded py-2.5 font-semibold text-sm transition-opacity disabled:opacity-50"
          style={{ background: "var(--accent)", color: "white" }}>
          {loading ? "Submitting..." : "Submit IssuePolicy Command →"}
        </button>
      </form>
    </div>
  );
}
