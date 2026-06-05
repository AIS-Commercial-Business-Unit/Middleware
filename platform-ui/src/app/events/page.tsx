export const dynamic = "force-dynamic";

const kafdropUrl = process.env.NEXT_PUBLIC_KAFDROP_URL || "http://localhost:9000";
const grafanaUrl = process.env.NEXT_PUBLIC_GRAFANA_URL || "http://localhost:3001";

export default function EventsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold mb-1">Live Event Stream</h1>
        <p className="text-sm" style={{ color: "var(--muted)" }}>
          All Kafka events flowing through the middleware platform in real time.
          View the full event stream in{" "}
          <a
            href={kafdropUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="underline"
            style={{ color: "var(--accent-light)" }}
          >
            Kafdrop
          </a>{" "}
          or structured logs in{" "}
          <a
            href={grafanaUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="underline"
            style={{ color: "var(--accent-light)" }}
          >
            Grafana Loki
          </a>
          .
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {[
          { topic: "policy.commands.issue-policy", owner: "PolicyIssuanceService", direction: "in" },
          { topic: "compliance.events.compliance-cleared", owner: "Platform.Compliance", direction: "out" },
          { topic: "customer.events.account-service-record-retrieved", owner: "CustomerIdentity", direction: "out" },
          { topic: "policy.events.issue-policy-requested", owner: "PolicyIssuanceService", direction: "out" },
          { topic: "integration.events.policy-admin-system-response-received", owner: "Platform.Integration", direction: "out" },
          { topic: "billing.events.billing-association-created", owner: "BillingFinance", direction: "out" },
          { topic: "customer.events.customer-updated", owner: "CustomerIdentity", direction: "out" },
          { topic: "policy.events.policy-issued", owner: "PolicyIssuanceService", direction: "out" },
          { topic: "notification.events.notification-dispatched", owner: "Platform.Notification", direction: "out" },
        ].map((t) => (
          <a
            key={t.topic}
            href={`${kafdropUrl}/topic/${t.topic}`}
            target="_blank"
            rel="noopener noreferrer"
            className="rounded-lg border p-4 hover:border-indigo-500 transition-colors"
            style={{ borderColor: "var(--border)", background: "var(--surface)" }}
          >
            <code className="text-xs font-mono block mb-1" style={{ color: "var(--accent-light)" }}>
              {t.topic}
            </code>
            <div className="flex items-center justify-between mt-2">
              <span className="text-xs" style={{ color: "var(--muted)" }}>
                {t.owner}
              </span>
              <span
                className="text-xs px-1.5 py-0.5 rounded font-mono"
                style={{
                  background: t.direction === "out" ? "#22c55e22" : "#6366f122",
                  color: t.direction === "out" ? "#22c55e" : "#818cf8",
                }}
              >
                {t.direction === "out" ? "publishes" : "consumes"}
              </span>
            </div>
          </a>
        ))}
      </div>
    </div>
  );
}
