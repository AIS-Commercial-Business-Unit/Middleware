"use client";

import { useState } from "react";

interface DocumentSummary {
  documentId: string;
  documentKey: string;
  sourceSystem: string;
  documentType: string;
  documentName: string;
  documentDate: string;
  policyNumber: string;
  status: string;
}

interface DocumentListResponse {
  requestId: string;
  policyNumber: string;
  documents: DocumentSummary[];
  partialResult: boolean;
}

interface DocumentResponse {
  requestId: string;
  documentId?: string;
  documentKey: string;
  sourceSystem: string;
  contentType: string;
  contentBase64: string;
  fileName: string;
  status?: string;
}

type SourceSystem = "AtWork" | "Mainframe";
type ListState = "idle" | "loading" | "success" | "error";
type DocumentState = "idle" | "loading" | "success" | "accepted" | "error";

const POLICY_CHIPS = ["POL-001-TEST", "POL-002-TEST", "POL-003-TEST", "POL-TIMEOUT"];

const pageStyle: React.CSSProperties = {
  display: "grid",
  gap: "1.5rem",
};

const gridStyle: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 420px), 1fr))",
  gap: "1.5rem",
  alignItems: "start",
};

const panelStyle: React.CSSProperties = {
  background: "var(--surface)",
  border: "1px solid var(--border)",
  borderRadius: 12,
  padding: "1.25rem",
  boxShadow: "0 10px 30px rgba(0, 0, 0, 0.18)",
};

const labelStyle: React.CSSProperties = {
  display: "block",
  marginBottom: "0.4rem",
  fontSize: "0.8rem",
  fontWeight: 600,
  color: "var(--muted)",
};

const inputStyle: React.CSSProperties = {
  width: "100%",
  minWidth: 0,
  background: "var(--bg)",
  color: "var(--text)",
  border: "1px solid var(--border)",
  borderRadius: 8,
  padding: "0.7rem 0.85rem",
  fontSize: "0.95rem",
  outline: "none",
};

const buttonStyle: React.CSSProperties = {
  background: "var(--accent)",
  color: "#fff",
  border: "none",
  borderRadius: 8,
  padding: "0.7rem 1rem",
  fontSize: "0.9rem",
  fontWeight: 700,
  cursor: "pointer",
  whiteSpace: "nowrap",
};

const disabledButtonStyle: React.CSSProperties = {
  ...buttonStyle,
  opacity: 0.65,
  cursor: "not-allowed",
};

const chipStyle: React.CSSProperties = {
  background: "transparent",
  color: "var(--text)",
  border: "1px solid var(--border)",
  borderRadius: 999,
  padding: "0.35rem 0.8rem",
  fontSize: "0.8rem",
  cursor: "pointer",
};

const tableCellStyle: React.CSSProperties = {
  padding: "0.7rem 0.75rem",
  borderBottom: "1px solid var(--border)",
  textAlign: "left",
  fontSize: "0.88rem",
  verticalAlign: "top",
};

function normalizeSourceLabel(sourceSystem: string): SourceSystem {
  const normalized = sourceSystem.trim().toLowerCase();
  if (normalized === "atwork" || normalized === "at_work") {
    return "AtWork";
  }
  return "Mainframe";
}

function formatDate(value: string): string {
  if (!value) {
    return "—";
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }
  return parsed.toISOString().slice(0, 10);
}

function estimateBytes(base64: string): number {
  const sanitized = base64.replace(/\s/g, "");
  const padding = sanitized.endsWith("==") ? 2 : sanitized.endsWith("=") ? 1 : 0;
  return Math.max(0, Math.floor((sanitized.length * 3) / 4) - padding);
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function isAcceptedStatus(status?: string): boolean {
  const normalized = status?.trim().toLowerCase();
  return normalized === "accepted" || normalized === "pending" || normalized === "processing";
}

async function readError(response: Response): Promise<string> {
  try {
    const data = (await response.json()) as { error?: string };
    return data.error ?? `HTTP ${response.status}`;
  } catch {
    return `HTTP ${response.status}`;
  }
}

export default function UC4Page() {
  const [policyNumber, setPolicyNumber] = useState("");
  const [listState, setListState] = useState<ListState>("idle");
  const [listError, setListError] = useState("");
  const [listResponse, setListResponse] = useState<DocumentListResponse | null>(null);

  const [documentKey, setDocumentKey] = useState("");
  const [sourceSystem, setSourceSystem] = useState<SourceSystem>("Mainframe");
  const [documentState, setDocumentState] = useState<DocumentState>("idle");
  const [documentError, setDocumentError] = useState("");
  const [documentResponse, setDocumentResponse] = useState<DocumentResponse | null>(null);

  const loadDocumentList = async (overridePolicyNumber?: string) => {
    const nextPolicyNumber = (overridePolicyNumber ?? policyNumber).trim();
    if (!nextPolicyNumber) {
      setListError("Policy number required");
      setListState("error");
      setListResponse(null);
      return;
    }

    setPolicyNumber(nextPolicyNumber);
    setListState("loading");
    setListError("");
    setListResponse(null);

    try {
      const response = await fetch(
        `/api/appraisals/list?policyNumber=${encodeURIComponent(nextPolicyNumber)}`
      );
      if (!response.ok) {
        setListError(await readError(response));
        setListState("error");
        return;
      }

      const data: DocumentListResponse = await response.json();
      setListResponse(data);
      setListState("success");
    } catch (error) {
      setListError(String(error));
      setListState("error");
    }
  };

  const retrieveDocument = async (override?: {
    documentKey: string;
    sourceSystem: SourceSystem;
  }) => {
    const nextDocumentKey = (override?.documentKey ?? documentKey).trim();
    const nextSourceSystem = override?.sourceSystem ?? sourceSystem;

    setDocumentKey(nextDocumentKey);
    setSourceSystem(nextSourceSystem);
    setDocumentError("");
    setDocumentResponse(null);

    if (!nextDocumentKey) {
      setDocumentError("Document key required");
      setDocumentState("error");
      return;
    }

    setDocumentState("loading");

    try {
      const response = await fetch(
        `/api/appraisals/document?documentKey=${encodeURIComponent(nextDocumentKey)}&sourceSystem=${encodeURIComponent(nextSourceSystem)}`
      );
      const data: DocumentResponse | { error?: string } = await response.json();

      if (!response.ok) {
        setDocumentError(
          typeof data === "object" && data && "error" in data && data.error
            ? data.error
            : `HTTP ${response.status}`
        );
        setDocumentState("error");
        return;
      }

      const documentData = data as DocumentResponse;
      setDocumentResponse(documentData);
      setDocumentState(
        response.status === 202 || isAcceptedStatus(documentData.status) ? "accepted" : "success"
      );
    } catch (error) {
      setDocumentError(String(error));
      setDocumentState("error");
    }
  };

  const handleDocumentPick = (document: DocumentSummary) => {
    const nextSourceSystem = normalizeSourceLabel(document.sourceSystem);
    setDocumentKey(document.documentKey);
    setSourceSystem(nextSourceSystem);
    void retrieveDocument({
      documentKey: document.documentKey,
      sourceSystem: nextSourceSystem,
    });
  };

  return (
    <div style={pageStyle}>
      <h1
        style={{
          margin: 0,
          fontSize: "1.75rem",
          fontWeight: 800,
          color: "var(--accent-light)",
        }}
      >
        UC4 — Appraisal Document Services
      </h1>

      <div style={gridStyle}>
        <section style={panelStyle}>
          <div style={{ display: "grid", gap: "1rem" }}>
            <div>
              <h2 style={{ margin: 0, fontSize: "1.1rem" }}>Policy Document List</h2>
            </div>

            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.6rem" }}>
              {POLICY_CHIPS.map((chip) => (
                <button
                  key={chip}
                  type="button"
                  style={chipStyle}
                  onClick={() => setPolicyNumber(chip)}
                >
                  {chip === "POL-TIMEOUT" ? "⏱ POL-TIMEOUT" : chip}
                </button>
              ))}
            </div>

            <div style={{ display: "flex", gap: "0.75rem", alignItems: "end", flexWrap: "wrap" }}>
              <div style={{ flex: "1 1 240px" }}>
                <label htmlFor="policy-number" style={labelStyle}>
                  Policy Number
                </label>
                <input
                  id="policy-number"
                  style={inputStyle}
                  type="text"
                  value={policyNumber}
                  placeholder="Enter policy number"
                  onChange={(event) => setPolicyNumber(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && listState !== "loading") {
                      void loadDocumentList();
                    }
                  }}
                />
              </div>
              <button
                type="button"
                style={listState === "loading" ? disabledButtonStyle : buttonStyle}
                disabled={listState === "loading"}
                onClick={() => void loadDocumentList()}
              >
                {listState === "loading" ? "Loading..." : "Get List"}
              </button>
            </div>

            {listState === "error" && (
              <div
                style={{
                  borderRadius: 8,
                  border: "1px solid rgba(239, 68, 68, 0.35)",
                  background: "rgba(127, 29, 29, 0.25)",
                  color: "#fca5a5",
                  padding: "0.85rem 1rem",
                  fontSize: "0.9rem",
                }}
              >
                {listError}
              </div>
            )}

            {listState === "success" && listResponse && (
              <div style={{ display: "grid", gap: "0.85rem" }}>
                {listResponse.partialResult && (
                  <div
                    style={{
                      borderRadius: 8,
                      border: "1px solid rgba(245, 158, 11, 0.35)",
                      background: "rgba(120, 53, 15, 0.25)",
                      color: "#fcd34d",
                      padding: "0.75rem 1rem",
                      fontSize: "0.88rem",
                    }}
                  >
                    Partial results returned.
                  </div>
                )}

                {listResponse.documents.length === 0 ? (
                  <div style={{ color: "var(--muted)", fontSize: "0.9rem" }}>
                    No documents found.
                  </div>
                ) : (
                  <div
                    style={{
                      overflowX: "auto",
                      border: "1px solid var(--border)",
                      borderRadius: 10,
                    }}
                  >
                    <table style={{ width: "100%", borderCollapse: "collapse" }}>
                      <thead style={{ background: "rgba(255, 255, 255, 0.02)" }}>
                        <tr>
                          {["#", "Document Name", "Type", "Date", "Source", " "].map((header) => (
                            <th
                              key={header}
                              style={{
                                ...tableCellStyle,
                                color: "var(--muted)",
                                fontSize: "0.78rem",
                                fontWeight: 700,
                                textTransform: "uppercase",
                                letterSpacing: "0.04em",
                              }}
                            >
                              {header}
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {listResponse.documents.map((document, index) => {
                          const cleanSource = normalizeSourceLabel(document.sourceSystem);
                          return (
                            <tr key={`${document.documentId}-${document.documentKey}-${index}`}>
                              <td style={{ ...tableCellStyle, color: "var(--muted)", width: "3rem" }}>
                                {index + 1}
                              </td>
                              <td style={tableCellStyle}>{document.documentName}</td>
                              <td style={tableCellStyle}>{document.documentType}</td>
                              <td style={tableCellStyle}>{formatDate(document.documentDate)}</td>
                              <td style={tableCellStyle}>{cleanSource}</td>
                              <td style={tableCellStyle}>
                                <button
                                  type="button"
                                  style={{ ...buttonStyle, padding: "0.5rem 0.85rem", fontSize: "0.82rem" }}
                                  onClick={() => handleDocumentPick(document)}
                                >
                                  Get Document
                                </button>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        </section>

        <section style={panelStyle}>
          <div style={{ display: "grid", gap: "1rem" }}>
            <div>
              <h2 style={{ margin: 0, fontSize: "1.1rem" }}>Retrieve Document</h2>
            </div>

            <div style={{ display: "grid", gap: "0.85rem" }}>
              <div>
                <label htmlFor="document-key" style={labelStyle}>
                  Document Key
                </label>
                <input
                  id="document-key"
                  style={inputStyle}
                  type="text"
                  value={documentKey}
                  placeholder="Enter document key"
                  onChange={(event) => setDocumentKey(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && documentState !== "loading") {
                      void retrieveDocument();
                    }
                  }}
                />
              </div>

              <div>
                <label htmlFor="source-system" style={labelStyle}>
                  Source System
                </label>
                <select
                  id="source-system"
                  style={inputStyle}
                  value={sourceSystem}
                  onChange={(event) => setSourceSystem(event.target.value as SourceSystem)}
                >
                  <option value="AtWork">AtWork</option>
                  <option value="Mainframe">Mainframe</option>
                </select>
              </div>

              <div>
                <button
                  type="button"
                  style={documentState === "loading" ? disabledButtonStyle : buttonStyle}
                  disabled={documentState === "loading"}
                  onClick={() => void retrieveDocument()}
                >
                  {documentState === "loading" ? "Loading..." : "Retrieve"}
                </button>
              </div>
            </div>

            {documentState === "error" && (
              <div
                style={{
                  borderRadius: 8,
                  border: "1px solid rgba(239, 68, 68, 0.35)",
                  background: "rgba(127, 29, 29, 0.25)",
                  color: "#fca5a5",
                  padding: "0.85rem 1rem",
                  fontSize: "0.9rem",
                }}
              >
                {documentError}
              </div>
            )}

            {documentState === "accepted" && documentResponse && (
              <div
                style={{
                  borderRadius: 10,
                  border: "1px solid rgba(245, 158, 11, 0.35)",
                  background: "rgba(120, 53, 15, 0.25)",
                  color: "#fcd34d",
                  padding: "1rem",
                  display: "grid",
                  gap: "0.35rem",
                }}
              >
                <strong>⏳ Request accepted — still processing (requestId: {documentResponse.requestId})</strong>
              </div>
            )}

            {documentState === "success" && documentResponse && (
              <div
                style={{
                  borderRadius: 10,
                  border: "1px solid rgba(34, 197, 94, 0.35)",
                  background: "rgba(20, 83, 45, 0.25)",
                  color: "#bbf7d0",
                  padding: "1rem",
                  display: "grid",
                  gap: "0.35rem",
                }}
              >
                <strong>✅ Document Retrieved</strong>
                <span>{documentResponse.fileName}</span>
                <span style={{ color: "#86efac", fontSize: "0.9rem" }}>
                  {formatBytes(estimateBytes(documentResponse.contentBase64))}
                </span>
              </div>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
