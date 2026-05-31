import { NextResponse } from "next/server";

type ServiceStatus = "healthy" | "unhealthy";

interface ServiceHealth {
  name: string;
  group: "java" | "dotnet" | "infra";
  status: ServiceStatus;
  responseMs: number | null;
  detail: string | null;
}

const SERVICES: Array<{ name: string; group: "java" | "dotnet" | "infra"; url: string }> = [
  // ── Java stack ────────────────────────────────────────────────────────────
  { name: "Policy Issuance",   group: "java",   url: "http://policy-issuance-service:8081/actuator/health" },
  { name: "Compliance",        group: "java",   url: "http://platform-compliance-service:8082/actuator/health" },
  { name: "Customer Identity", group: "java",   url: "http://customer-identity-service:8083/actuator/health" },
  { name: "Integration",       group: "java",   url: "http://platform-integration-service:8084/actuator/health" },
  { name: "Billing Finance",   group: "java",   url: "http://billing-finance-service:8085/actuator/health" },
  { name: "Notification",      group: "java",   url: "http://platform-notification-service:8086/actuator/health" },
  { name: "File Processing",   group: "java",   url: "http://platform-file-processing-service:8087/actuator/health" },
  { name: "PRS Appraisal",     group: "java",   url: "http://prs-appraisal-service:8090/actuator/health" },
  // ── .NET stack ────────────────────────────────────────────────────────────
  { name: "Policy Issuance",   group: "dotnet", url: "http://dotnet-policy-issuance:8181/health" },
  { name: "Compliance",        group: "dotnet", url: "http://dotnet-platform-compliance:8182/health" },
  { name: "Customer Identity", group: "dotnet", url: "http://dotnet-customer-identity:8183/health" },
  { name: "Integration",       group: "dotnet", url: "http://dotnet-platform-integration:8184/health" },
  { name: "Billing Finance",   group: "dotnet", url: "http://dotnet-billing-finance:8185/health" },
  { name: "Notification",      group: "dotnet", url: "http://dotnet-platform-notification:8186/health" },
  { name: "File Processing",   group: "dotnet", url: "http://dotnet-file-processing:8187/health" },
  { name: "Kafka Bridge",      group: "dotnet", url: "http://dotnet-kafka-bridge:8188/health" },
  { name: "PRS Appraisal",     group: "dotnet", url: "http://dotnet-prs-appraisal:8189/health" },
  // ── Infrastructure ────────────────────────────────────────────────────────
  { name: "Loki",              group: "infra",  url: "http://loki:3100/ready" },
  { name: "Prometheus",        group: "infra",  url: "http://prometheus:9090/-/ready" },
  { name: "Grafana",           group: "infra",  url: "http://grafana:3000/api/health" },
  { name: "Kafka UI",          group: "infra",  url: "http://kafka-ui:8080/actuator/health" },
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
