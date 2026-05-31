# UC4 Appraisal Service — Test Data

## Demo Scenarios

⚠️ All producer codes and control codes below are FABRICATED for demo purposes.
Real data must be provided by the PRS team.

### Test Policies (Seeded in StubCustomerDBGateway)

| Policy Number | Producer Code | Control Code | Expected UW Assignment | Suspense Days |
|---------------|---------------|--------------|----------------------|---------------|
| POL-12345 | REPLACE_ME_PROD001 | UA-001 | UA | 45 (ASSUMED) |
| POL-12346 | REPLACE_ME_PROD002 | UST-001 | UST | 14 (ASSUMED) |
| POL-12347 | REPLACE_ME_PROD003 | UA-001 | UA | 45 (ASSUMED) |
| POL-12348 | REPLACE_ME_PROD004 | UA-001 | UA | 45 (ASSUMED) |
| POL-12349 | REPLACE_ME_PROD005 | UST-001 | UST | 14 (ASSUMED) |

## Test Scenarios (Postman)

POST http://localhost:8090/api/appraisal/status-update

### Scenario 1: Happy Path — UA Assignment
```json
{
  "inspectionId": "INS-001",
  "policyNumber": "POL-12345",
  "statusCode": 6,
  "inspectionTypeCode": "A",
  "REPLACE_ME_additionalRiskIDFields": null
}
```
Expected: UW=UA, suspenseDays=45, status=Completed

### Scenario 2: Happy Path — UST Assignment
```json
{
  "inspectionId": "INS-002",
  "policyNumber": "POL-12346",
  "statusCode": 6,
  "inspectionTypeCode": "B",
  "REPLACE_ME_additionalRiskIDFields": null
}
```
Expected: UW=UST, suspenseDays=14, status=Completed

### Scenario 3: Inspection Type I — UA Assignment
```json
{
  "inspectionId": "INS-003",
  "policyNumber": "POL-12347",
  "statusCode": 6,
  "inspectionTypeCode": "I",
  "REPLACE_ME_additionalRiskIDFields": null
}
```
Expected: UW=UA, suspenseDays=45, status=Completed

### Scenario 4: Timeout (for demo — requires shortening timeout in application.yml)
```json
{
  "inspectionId": "INS-004",
  "policyNumber": "POL-12348",
  "statusCode": 6,
  "inspectionTypeCode": "A",
  "REPLACE_ME_additionalRiskIDFields": null
}
```
⚠️ DEMO NOTE: To demo timeout, temporarily set appraisal.saga.timeout-minutes=1 in application.yml.

### Scenario 5: StatusCode=15 (Completed)
```json
{
  "inspectionId": "INS-005",
  "policyNumber": "POL-12345",
  "statusCode": 15,
  "inspectionTypeCode": "A",
  "REPLACE_ME_additionalRiskIDFields": null
}
```
Expected: Masterpiece Transaction 90 stub called, status=Completed

## Get Saga State
GET http://localhost:8090/api/appraisal/sagas
GET http://localhost:8090/api/appraisal/sagas/{correlationId}

## Health Check
GET http://localhost:8090/actuator/health

## Demo Gaps (Items Requiring PRS Team Input)

1. ⚠️ Real RiskID IBM MQ message schema (field names, data types)
2. ⚠️ Real PLUW WCF-WSHTTP API contract (SOAP schema)
3. ⚠️ Real PLAPR stored procedure name and table schema
4. ⚠️ Real Masterpiece Transaction 90 (PLIPQP90) request/response format
5. ⚠️ Real CustomerDB stored procedure name and producer code structure
6. ⚠️ UW determination rules — actual rule codes for UA vs UST routing
7. ⚠️ Suspense days — 45 for UA and 14 for UST are ASSUMED
8. ⚠️ Other status codes besides 6 and 15 — unknown how many exist
9. ⚠️ Actual producer codes for demo policies
