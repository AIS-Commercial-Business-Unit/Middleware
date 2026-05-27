import type { NextRequest } from "next/server";

export type Backend = "java" | "dotnet";

function resolveBackend(cookieValue?: string | null): Backend {
  if (cookieValue === "dotnet" || cookieValue === "java") return cookieValue;
  return process.env.ACTIVE_BACKEND === "dotnet" ? "dotnet" : "java";
}

export function getActiveBackend(): Backend {
  return resolveBackend(undefined);
}

export function getActiveBackendFromRequest(req: NextRequest): Backend {
  const cookie = req.cookies.get("active-backend")?.value;
  return resolveBackend(cookie);
}

export function getPolicyIssuanceServiceUrl(backend?: Backend): string {
  const b = backend ?? getActiveBackend();
  return b === "dotnet"
    ? process.env.DOTNET_POLICY_ISSUANCE_URL ?? "http://dotnet-policy-issuance:8181"
    : process.env.POLICY_ISSUANCE_SERVICE_URL ?? "http://policy-issuance-service:8081";
}

export function getFileProcessingServiceUrl(backend?: Backend): string {
  const b = backend ?? getActiveBackend();
  return b === "dotnet"
    ? process.env.DOTNET_FILE_PROCESSING_URL ?? "http://dotnet-file-processing:8187"
    : process.env.FILE_PROCESSING_SERVICE_URL ?? "http://platform-file-processing-service:8087";
}

export function getClientActiveBackendLabel(): string {
  return process.env.NEXT_PUBLIC_ACTIVE_BACKEND === "dotnet" ? ".NET Stack" : "Java Stack";
}
