# Running the Demos

This guide explains what each demo shows, what to watch, and how to verify everything is working — without reference to any specific technology stack.

---

## Before You Start

1. Ensure Rancher Desktop (or Docker Desktop) is running
2. From the repository root, run:

```bash
docker compose up --build
```

3. Wait ~60 seconds for all services to initialize (Kafka and SQL Server take the longest)
4. Verify everything is up:

```bash
docker compose ps
```

All services should show `healthy` or `running`. Infrastructure containers (Kafka, MongoDB, SQL Server, Grafana) take longest.

---

## Platform UI

Open **http://localhost:3000** to access the Platform UI.

The UI is backend-switchable — it can talk to either the Java or .NET stack:

| Page | Purpose |
|---|---|
| Home | Submit a UC1 Policy Issuance command |
| Saga Explorer (`/saga/<id>`) | Watch a saga advance step-by-step in real time |
| Live Events | Stream of Kafka events as they flow through the system |
| Operations | Grafana links, backend status, queue depths |

---

## UC1 — Policy Issuance Saga

**What this demonstrates:**
- Long-running saga coordinating 6 services without direct service-to-service calls
- Content-Based Router sending policies to the correct Policy Admin System
- Parallel step execution (billing + customer update run simultaneously)
- Observable distributed trace: one request → one trace spanning all services

### How to Run UC1

**Option A — Platform UI:**
1. Open http://localhost:3000
2. Select a Policy Type Code (e.g., `1` for Commercial Lines)
3. Click **Submit IssuePolicy Command**
4. You are redirected to the Saga Explorer for that `issuanceId`

**Option B — curl:**

```bash
# Java stack (port 8081)
curl -X POST http://localhost:8081/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":"Jane Smith","coverageAmount":250000}'

# .NET stack (port 8181)
curl -X POST http://localhost:8181/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":"Jane Smith","coverageAmount":250000}'
```

**Option C — E2E test script:**

```powershell
.\scripts\test-e2e.ps1 -Stack java    # Java only
.\scripts\test-e2e.ps1 -Stack dotnet  # .NET only
.\scripts\test-e2e.ps1 -Stack both    # Both stacks
```

### What to Watch

**Saga state progression** (via Saga Explorer or `docker logs`):

```
Initiated
  → AwaitingCompliance       [waiting for RSK3X3 sanctions check]
  → AwaitingAccountRecord    [waiting for ERM7X1 account lookup]
  → AwaitingPAS              [waiting for DuckCreek / ForeFront policy creation]
  → PASConfirmed             [PAS responded — start billing + customer update in parallel]
  → Completed                [both billing and customer update confirmed]
```

**Expected timing:** ~6–10 seconds end-to-end in the demo environment.

**Policy Admin System routing** (based on `policyTypeCode`):

| Code | Routes to |
|---|---|
| 1, 2, 3, 4, 42–47 | DuckCreek Commercial stub |
| 5–9 | DuckCreek Personal stub |
| 10, 12, 14, 17, 18 | ForeFront stub |

### Observability for UC1

**Structured Logs (Grafana → Explore → Loki):**

```
# Filter to one saga's journey across all services:
{app=~"policy.*|platform.*|billing.*|customer.*|dotnet.*"} 
  | json 
  | issuanceId = "<your-issuanceId>"
```

**Distributed Trace (Grafana → Explore → Tempo):**
- Search by service name: `policy-issuance-service` or `dotnet-policy-issuance`
- Or paste the `traceId` from a log line
- You'll see one tree spanning all 6 services

**Kafka Topics (Kafdrop — http://localhost:9000):**
- `policy.commands.issue-policy`
- `compliance.events.compliance-cleared`
- `customer.events.account-service-record-retrieved`
- `integration.events.policy-admin-system-response-received`
- `billing.events.billing-association-created`
- `policy.events.policy-issued`

---

## UC3 — Automated Renewal Batch Processing

**What this demonstrates:**
- File-based batch processing (CSV file drop → transform → dispatch)
- Batch generation of renewal data for testing
- Triggering multiple policy issuance sagas from a single file
- Observing batch-level metrics alongside individual saga traces

### How to Run UC3

**Step 1 — Generate test renewal data:**

```bash
# Java stack
curl -X POST "http://localhost:8087/api/v1/renewals/generate?count=5"

# .NET stack
curl -X POST "http://localhost:8187/api/v1/renewals/generate?count=5"
```

This creates a CSV file in the renewal data volume with `count` renewal records.

**Step 2 — Watch file processing:**

The file processor polls for new CSV files every 10 seconds. Once it finds the file:
1. Parses each renewal record
2. Dispatches one `IssuePolicy` command per record
3. Each command starts an independent saga

**Step 3 — Monitor via logs:**

```bash
# Java
docker logs java-file-processing -f

# .NET
docker logs dotnet-file-processing -f
```

Expected output:
```
Renewal batch file detected: renewals-2025-01-01.csv
Processing record 1/5: policyNumber=POL-001 applicant=John Doe
Dispatched IssuePolicy command: issuanceId=abc-123
Processing record 2/5: policyNumber=POL-002 applicant=Jane Smith
...
Batch complete: 5 dispatched, 0 errors
```

**Step 4 — Watch sagas complete:**

```bash
# Check how many sagas are in progress / completed
curl http://localhost:8081/api/v1/policies/status/summary   # Java
curl http://localhost:8181/api/v1/policies/status/summary   # .NET
```

### Observability for UC3

**Batch-level logs:**

```
{app="java-file-processing"} | json | filename=~"renewals.*"
```

**Individual saga traces:**
Each of the 5 dispatched issuance commands produces its own trace. Search in Tempo by service `policy-issuance-service` and filter by time window.

---

## Side-by-Side Comparison

To run both stacks simultaneously and compare:

```bash
docker compose up --build

# Submit to Java stack
curl -X POST http://localhost:8081/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":"Java User","coverageAmount":100000}'

# Submit to .NET stack
curl -X POST http://localhost:8181/api/v1/policies/issue \
  -H "Content-Type: application/json" \
  -d '{"policyTypeCode":"1","applicantName":" .NET User","coverageAmount":100000}'
```

Both produce structured logs in Loki and traces in Tempo. Use the `app` label in Loki to distinguish stacks:
- Java: `{app=~"policy-issuance.*|platform-compliance.*|..."}`
- .NET: `{app=~"dotnet-.*"}`

---

## What the Demos Prove

| Claim | Evidence |
|---|---|
| Long-running saga works across 6 services | Saga Explorer shows all 9 state transitions |
| Services communicate only via events | No direct HTTP calls between domain services in the logs |
| Content-Based Router sends to correct PAS | Log line: `Routing to DuckCreek Commercial — policyTypeCode=1` |
| Parallel steps work correctly | Billing + CustomerUpdate logs both appear before `COMPLETED` |
| File-based batch processing works | 5 sagas complete after single CSV file drop |
| Both stacks are observable | Loki + Tempo show identical telemetry for Java and .NET |
| Distributed trace spans all services | One `traceId` visible across all service spans in Tempo |

---

## Running the Full E2E Test Suite

```powershell
# Run everything and report pass/fail
.\scripts\test-e2e.ps1 -Stack both -Verbose

# Expected: 60 pass, 0 fail, 0 warnings
```

The test script covers:
- UC1 saga (Java + .NET)
- UC3 batch processing (Java + .NET)
- Platform UI backend info endpoint
- Grafana / Loki / Tempo health checks

---

## Port Reference

| Service | Port |
|---|---|
| **Platform UI** | 3000 |
| **Grafana** (admin / admin) | 3001 |
| **Kafdrop** (Kafka UI) | 9000 |
| **Mongo Express** | 8888 |
| **Prometheus** | 9090 |
| Java — policy-issuance | 8081 |
| Java — compliance | 8082 |
| Java — customer-identity | 8083 |
| Java — integration | 8084 |
| Java — billing | 8085 |
| Java — notification | 8086 |
| Java — file-processing | 8087 |
| .NET — policy-issuance | 8181 |
| .NET — compliance | 8182 |
| .NET — customer-identity | 8183 |
| .NET — integration | 8184 |
| .NET — billing | 8185 |
| .NET — notification | 8186 |
| .NET — file-processing | 8187 |
| .NET — kafka-bridge | 8188 |
| DuckCreek Commercial stub | 9001 |
| DuckCreek Personal stub | 9002 |
| ForeFront stub | 9003 |
| RSK3X3 (Compliance) stub | 9004 |
| ERM7X1 (Account) stub | 9005 |
| CRM40X1 (Customer) stub | 9006 |
| CRM19X1 (Billing) stub | 9007 |
