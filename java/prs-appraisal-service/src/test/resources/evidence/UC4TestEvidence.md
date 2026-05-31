# UC4 Test Evidence — GetAppraisalList & GetAppraisalDocument

> **Service under test:** `prs-appraisal-service` (port 8090)  
> **Test date:** 2026-05-30  
> **Author:** QA (squad agent)  
> **Status:** Tests written — evidence to be filled after first run against docker stack

---

## Test Environment

| Item | Value |
|---|---|
| Stack | `docker compose up` (full stack including `activemq-artemis` + `deipde07-mq-simulator`) |
| Service under test | `prs-appraisal-service` port 8090 |
| DEIPDE07 simulator | `deipde07-mq-simulator` port 9020 |
| ActiveMQ Artemis | port 61616 (JMS), 8161 (web console) |
| Broker URL | `tcp://localhost:61616` |
| Request queue | `MQP.REQUESTQUEUE.1` |
| Response queue | `MQP.RESPONSEQUEUE.1` |
| Test framework | JUnit 5 + AssertJ + Spring `RestTemplate` |

---

## Pre-Run Checklist

1. `docker compose up` — wait for all services healthy (activemq-artemis has a 30s start period)
2. Verify: `curl http://localhost:9020/actuator/health` → `{"status":"UP"}`
3. Verify: `curl http://localhost:8090/actuator/health` → `{"status":"UP"}`
4. Verify Artemis console: `http://localhost:8161/console/artemis` → queues visible
5. Run: `mvn test -pl prs-appraisal-service -Dgroups=integration`

---

## GetAppraisalList Tests

| Test ID | Test Method | Input | Expected | Status |
|---|---|---|---|---|
| SC-001 | `happyPath_ThreeRecordsFromBothSources` | `policyNumber=POL-001-TEST` | HTTP 200, items≥3, partialResult=false, numeric + RiskID keys present, no duplicate (streetAdr+policyQuoteNbr) | PENDING_RUN |
| SC-002 | `deipde07ZeroResults_AtWorkOnlyResponse` | `policyNumber=POL-002-TEST` | HTTP 200, all items from @Work (_RiskID_ keys), partialResult=false | PENDING_RUN |
| SC-003 | `singleRecord_deipde07` | `policyNumber=POL-003-TEST` | HTTP 200, exactly 1 numeric DEIPDE07 key, partialResult=false | PENDING_RUN |
| SC-004 | `timeout_deipde07_returnsPartialResult` | `policyNumber=POL-TIMEOUT` | HTTP 200, partialResult=true, no numeric DEIPDE07 keys, completes within 40s | PENDING_RUN |
| SC-005 | `unknownPolicy_returnsEmptyList` | `policyNumber=POL-UNKNOWN-99999` | HTTP 200, items=[], partialResult=false | PENDING_RUN |
| SC-006 | `missingPolicyNumber_returnsBadRequest` | `policyNumber=""` | HTTP 400 | PENDING_RUN |

---

## GetAppraisalDocument Tests

| Test ID | Test Method | Input | Expected | Status |
|---|---|---|---|---|
| DC-001 | `deipde07SmallPdf_chunksAggregated` | `documentKey=12345678901` | HTTP 200, base64Pdf non-empty, decodable, no \r\n, contentType=application/pdf | PENDING_RUN |
| DC-002 | `deipde07LargePdf_chunksAggregated` | `documentKey=98765432109876` | HTTP 200, base64Pdf.length()>1000, decodable, no \r\n | PENDING_RUN |
| DC-003 | `riskIdWcfInsured_directCall` | `documentKey=DOC_RiskID_I_TEST001` | HTTP 200, base64Pdf non-empty, decodable | PENDING_RUN |
| DC-004 | `riskIdWcfAgent_directCall` | `documentKey=DOC_RiskID_A_TEST002` | HTTP 200, base64Pdf non-empty, decodable | PENDING_RUN |
| DC-005 | `unknownDocumentKey_returnsBadRequest` | `documentKey=NOT-A-VALID-KEY` | HTTP 400 or 5xx | PENDING_RUN |
| DC-006 | `numericKeyNotInFixture_handledGracefully` | `documentKey=99999999999` | HTTP 404 or 5xx (not silent 200 empty) | PENDING_RUN |

---

## Expected Structured Log Assertions

After running the tests, verify these log entries in Grafana/Loki (filter: `app=prs-appraisal-service`):

### SC-001 (POL-001-TEST)
```
level=INFO  message="GetAppraisalList start"  policyNumber=POL-001-TEST  correlationId=<uuid>
level=INFO  message="ScatterGather fanout started"  atWork=dispatched  deipde07=dispatched
level=INFO  message="GetAppraisalList complete"  itemCount>=3  partialResult=false
```

### SC-004 (POL-TIMEOUT — 30-second timeout path)
```
level=WARN  message="DEIPDE07 MQ timeout"  policyNumber=POL-TIMEOUT  partialResult=true
level=INFO  message="GetAppraisalList complete"  partialResult=true
```

### DC-001 (12345678901 — 8-chunk small PDF)
```
level=INFO  message="GetAppraisalDocument start"  documentKey=12345678901  route=DEIPDE07_MQ
level=INFO  message="PdfChunkProcessor: END-OF-DOCUMENT received"  totalChunks=8
level=INFO  message="GetAppraisalDocument complete"  documentKey=12345678901
```

### DC-002 (98765432109876 — 200-chunk large PDF)
```
level=INFO  message="PdfChunkProcessor: END-OF-DOCUMENT received"  totalChunks=200
level=INFO  message="GetAppraisalDocument complete"  documentKey=98765432109876
```

---

## Evidence Fill-In Table (complete after first docker stack run)

| Test | Actual HTTP Status | Actual Item Count | Actual partialResult | Log Lines Observed | Pass/Fail | Run Date |
|---|---|---|---|---|---|---|
| SC-001 | | | | | | |
| SC-002 | | | | | | |
| SC-003 | | | | | | |
| SC-004 | | | | | | |
| SC-005 | | | | | | |
| SC-006 | | | | | | |
| DC-001 | | | | | | |
| DC-002 | | | | | | |
| DC-003 | | | | | | |
| DC-004 | | | | | | |
| DC-005 | | | | | | |
| DC-006 | | | | | | |

---

## Business Rules Covered

| Rule | Covered By |
|---|---|
| BR-APR-001: Both backends dispatched simultaneously (not sequentially) | SC-001 timing (< sum of individual call times) |
| BR-APR-002: DEIPDE07 timeout → partialResult=true | SC-004 |
| BR-APR-003: Deduplication by (streetAdr + policyQuoteNbr) | SC-001 deduplication assertion |
| BR-APR-004: DocumentKey routing is deterministic — key format is sole criterion | DC-001 (numeric), DC-003 (_RiskID_I), DC-004 (_RiskID_A) |
| BR-APR-005: base64 PDF only — no raw bytes | DC-001 / DC-002 (Base64.getDecoder() assertion) |
| BR-APR-006: EBCDIC CRLF artifacts stripped from chunks | DC-001 / DC-002 (no \r\n assertion) |
| BR-APR-007: DEIPDE07 document timeout → error, not partial doc | DC-006 (simulator not-found path) |
