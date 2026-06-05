import { NextResponse } from "next/server";

type ServiceStatus = "healthy" | "unhealthy";

interface ServiceHealth {
  name: string;
  group: "java" | "dotnet" | "infra";
  status: ServiceStatus;
  responseMs: number | null;
  detail: string | null;
}

// In K8s, services resolve as middleware-{name}:80; in Docker Compose they use
// bare names with their container port. Environment variables allow overriding.
const javaBase = (svc: string) =>
  process.env[`DEMO_HEALTH_${svc}`] || `http://${svc}:8081`;
const dotnetBase = (svc: string) =>
  process.env[`DEMO_HEALTH_${svc}`] || `http://${svc}:8181`;

const SERVICES: Array<{ name: string; group: "java" | "dotnet" | "infra"; url: string }> = [
  // ── Java stack ────────────────────────────────────────────────────────────
  { name: "Policy Issuance",   group: "java",   url: `${javaBase("POLICY_ISSUANCE")}/actuator/health` },
  { name: "Compliance",        group: "java",   url: `${javaBase("PLATFORM_COMPLIANCE")}/actuator/health` },
  { name: "Customer Identity", group: "java",   url: `${javaBase("CUSTOMER_IDENTITY")}/actuator/health` },
  { name: "Integration",       group: "java",   url: `${javaBase("PLATFORM_INTEGRATION")}/actuator/health` },
  { name: "Billing Finance",   group: "java",   url: `${javaBase("BILLING_FINANCE")}/actuator/health` },
  { name: "Notification",      group: "java",   url: `${javaBase("PLATFORM_NOTIFICATION")}/actuator/health` },
  { name: "File Processing",   group: "java",   url: `${javaBase("PLATFORM_FILE_PROCESSING")}/actuator/health` },
  { name: "PRS Appraisal",     group: "java",   url: `${javaBase("PRS_APPRAISAL")}/actuator/health` },
  // ── .NET stack ────────────────────────────────────────────────────────────
  { name: "Policy Issuance",   group: "dotnet", url: `${dotnetBase("DOTNET_POLICY_ISSUANCE")}/health` },
  { name: "Compliance",        group: "dotnet", url: `${dotnetBase("DOTNET_PLATFORM_COMPLIANCE")}/health` },
  { name: "Customer Identity", group: "dotnet", url: `${dotnetBase("DOTNET_CUSTOMER_IDENTITY")}/health` },
  { name: "Integration",       group: "dotnet", url: `${dotnetBase("DOTNET_PLATFORM_INTEGRATION")}/health` },
  { name: "Billing Finance",   group: "dotnet", url: `${dotnetBase("DOTNET_BILLING_FINANCE")}/health` },
  { name: "Notification",      group: "dotnet", url: `${dotnetBase("DOTNET_PLATFORM_NOTIFICATION")}/health` },
  { name: "File Processing",   group: "dotnet", url: `${dotnetBase("DOTNET_FILE_PROCESSING")}/health` },
  { name: "Kafka Bridge",      group: "dotnet", url: `${dotnetBase("DOTNET_KAFKA_BRIDGE")}/health` },
  { name: "PRS Appraisal",     group: "dotnet", url: `${dotnetBase("DOTNET_PRS_APPRAISAL")}/health` },
  // ── Infrastructure ────────────────────────────────────────────────────────
  { name: "Loki",              group: "infra",  url: `${process.env.LOKI_URL || "http://loki:3100"}/ready` },
  { name: "Prometheus",        group: "infra",  url: `${process.env.PROMETHEUS_URL || "http://prometheus:9090"}/-/ready` },
  { name: "Grafana",           group: "infra",  url: `${process.env.GRAFANA_URL || "http://grafana:3000"}/api/health` },
  { name: "Kafdrop",           group: "infra",  url: `${process.env.KAFDROP_URL || "http://kafdrop:9000"}/` },
];

async function checkService(
  svc: (typeof SERVICES)[number]
): Promise<ServiceHealth> {
  const start = Date.now();
  try {
    const res = await fetch(svc.url, {
      next: { revalidate: 0 },
      signal: AbortSignal.timeout(3000),
    });
    const responseMs = Date.now() - start;
    if (res.ok) {
      let detail: string | null = null;
      try {
        const body = await res.json();
        if (typeof body?.status === "string") detail = body.status;
      } catch {
        // non-JSON body (Loki returns "ready", Prometheus returns plain text)
        try {
          const text = await res.clone().text();
          detail = text.trim().slice(0, 40) || null;
        } catch { /* ignore */ }
      }
      return { name: svc.name, group: svc.group, status: "healthy", responseMs, detail };
    }
    return { name: svc.name, group: svc.group, status: "unhealthy", responseMs, detail: `HTTP ${res.status}` };
  } catch (err) {
    return {
      name: svc.name,
      group: svc.group,
      status: "unhealthy",
      responseMs: null,
      detail: err instanceof Error ? err.message.slice(0, 60) : "Timeout",
    };
  }
}

export async function GET() {
  const services = await Promise.all(SERVICES.map(checkService));
  const healthy = services.filter((s) => s.status === "healthy").length;
  return NextResponse.json({
    checkedAt: new Date().toISOString(),
    total: services.length,
    healthy,
    unhealthy: services.length - healthy,
    services,
  });
}
