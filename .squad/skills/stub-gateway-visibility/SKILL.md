---
name: "stub-gateway-visibility"
description: "How to make gateway stubs observable and demo-honest using STUBBED log markers and gap documentation"
domain: "testing"
confidence: "medium"
source: "earned"
---

## Context

When demoing BizTalk replacement workflows, all external integration points (legacy MQ, stored procedures, SOAP services) are initially stubbed. The demo must be honest about what is and isn't real without losing the audience.

## Patterns

### 1. ⚠️ STUBBED Log Markers

Every gateway stub implementation MUST emit a structured log entry with `⚠️ STUBBED` in the message body:

```java
// Java (SLF4J)
log.warn("⚠️ STUBBED: PLAPRGateway.updatePLAPR()",
    kv("correlationId", correlationId),
    kv("inspectionId", inspectionId),
    kv("stubNote", "MongoDB simulation — real impl uses SQL stored procedure"));
```

```csharp
// .NET (Serilog)
_logger.Warning("⚠️ STUBBED: {Gateway}.{Method}()",
    "PLAPRGateway", "UpdatePLAPR",
    new { CorrelationId = correlationId, StubNote = "MongoDB simulation" });
```

This allows the demo presenter to point at a specific log line and say: "Here is exactly where the real integration boundary is."

### 2. Two-Section Test Scenario Structure

For any BizTalk replacement feature, produce test scenarios in two distinct sections:

**Section A — Architecture Test Scenarios:** Test patterns that are fully verifiable today:
- Saga state transitions (InProgress → Completed / Failed)
- Content-based routing (StatusCode → correct sub-workflow)
- Parallel join behavior
- EDA_FLOW structured log properties
- DLQ handling and retry counts

**Section B — Demo Gap Scenarios:** Explicit documentation of what cannot be verified without real data:
- Each gap gets an ID (SC-GAP-001, SC-GAP-002, etc.)
- Risk level: 🔴 HIGH / 🟡 MEDIUM / 🟢 LOW
- Specific question for domain expert
- Description of what the stub does vs. what the real system would do

### 3. Gap Summary Table at End of Scenarios Doc

Always close the test scenarios document with a gap summary table:

```markdown
| Gap ID | What's Missing | Risk Level | Needed From |
|--------|---------------|------------|-------------|
| SC-GAP-001 | PLAPR table schema | 🔴 HIGH | PRS Developer |
```

### 4. Demo Script STOP POINTS

In the demo script, mark explicit stop points where the presenter MUST surface a gap:

```
> ⛔ **DEMO GAP CALLOUT:** "See this ⚠️ STUBBED marker on the CustomerDBGateway call?
> The real CustomerDB will have different response data, and that data will affect the 
> UW routing decision. We need the real producer cross-reference lookup from PRS."
```

## Examples

- `.docs/uc4-test-scenarios.md` — Section 5 (Demo Gap Scenarios), Section 6 (Log Verification Checklist), Section 8 (Summary Table)
- `.docs/uc4-demo-script.md` — `⛔ DEMO GAP CALLOUT` blocks in Parts 2, 3, 4

## Anti-Patterns

- ❌ Presenting stub behavior as if it were real without disclosure — loses credibility when domain expert notices a mismatch
- ❌ "Demos fine locally" without identifying which parts are stubs vs. real integration
- ❌ Omitting the `⚠️ STUBBED` marker — makes it impossible to distinguish real gateway calls from fake ones in log output
- ❌ Mixing architecture verification and business rule verification into the same test scenario — they have different confidence levels and different prerequisites
