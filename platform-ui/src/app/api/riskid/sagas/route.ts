import { NextResponse } from "next/server";

const INTEGRATION_SERVICE_URL =
  process.env.INTEGRATION_SERVICE_URL ?? "http://platform-integration-service:8084";

export async function GET() {
  try {
    const res = await fetch(`${INTEGRATION_SERVICE_URL}/api/appraisal/sagas`, {
      next: { revalidate: 0 },
      signal: AbortSignal.timeout(3000),
    });

    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    return NextResponse.json(data);
  } catch {
    // Appraisal service not yet implemented — return seeded mock sagas for demo
    const now = new Date();
    const ago = (seconds: number) =>
      new Date(now.getTime() - seconds * 1000).toISOString();

    return NextResponse.json({
      isMockData: true,
      sagas: [
        {
          sagaId: "saga-ins-001",
          correlationId: "saga-ins-001",
          inspectionId: "INS-001",
          policyNumber: "POL-12345",
          statusCode: 6,
          inspectionTypeCode: "A",
          status: "Completed",
          subWorkflow: "StatusCode6UW",
          uwAssignment: "UA",
          producerCode: "PROD-001",
          isMockData: true,
          receivedAt: ago(180),
          completedAt: ago(30),
          timeoutAt: null,
          gatewayCalls: [
            { name: "CustomerDBGateway", label: "Producer cross-reference lookup", status: "Completed", stubbed: true, calledAt: ago(170), responseAt: ago(165) },
            { name: "PLUWGateway (Create Appraisal)", label: "Create appraisal record in PLUW", status: "Completed", stubbed: true, calledAt: ago(160), responseAt: ago(155) },
            { name: "UWDetermination", label: "Determine UW assignment (UA vs UST)", status: "Completed", stubbed: true, calledAt: ago(160), responseAt: ago(150) },
            { name: "PLUWGateway (Suspense)", label: "Create 45-day suspense item", status: "Completed", stubbed: true, calledAt: ago(145), responseAt: ago(140) },
            { name: "PLUWGateway (@Work)", label: "Update @Work status", status: "Completed", stubbed: true, calledAt: ago(135), responseAt: ago(130) },
            { name: "PLAPRGateway", label: "Update PLAPR staging DB", status: "Completed", stubbed: true, calledAt: ago(125), responseAt: ago(120) },
          ],
          events: [
            { timestamp: ago(180), event: "RiskIDStatusUpdateReceived", details: "StatusCode=6, InspectionType=A", isGap: false },
            { timestamp: ago(178), event: "ProducerLookupRequested", details: "PolicyNumber=POL-12345", isGap: false },
            { timestamp: ago(165), event: "ProducerCrossReferenceRetrieved", details: "ProducerCode=PROD-001 ⚠️ MOCK DATA — Real RiskID schema TBD", isGap: true },
            { timestamp: ago(162), event: "StatusCode6UWSagaStarted", details: "Parallel: PLUW creation + UW determination", isGap: false },
            { timestamp: ago(150), event: "UWAssignmentDetermined", details: "Assignment=UA ⚠️ MOCK DATA — Real rule codes TBD", isGap: true },
            { timestamp: ago(145), event: "PLUWAppraisalCreated", details: "PLUW appraisal record created (stub)", isGap: false },
            { timestamp: ago(140), event: "SuspenseItemCreated", details: "45-day suspense (rule: InspectionType=A) ⚠️ MOCK DATA — Real suspense rules TBD", isGap: true },
            { timestamp: ago(120), event: "AppraisalUnderwriterAssigned", details: "UW=UA, all downstream systems updated", isGap: false },
          ],
        },
        {
          sagaId: "saga-ins-002",
          correlationId: "saga-ins-002",
          inspectionId: "INS-002",
          policyNumber: "POL-67890",
          statusCode: 6,
          inspectionTypeCode: "B",
          status: "Completed",
          subWorkflow: "StatusCode6UW",
          uwAssignment: "UST",
          producerCode: "PROD-002",
          isMockData: true,
          receivedAt: ago(90),
          completedAt: ago(15),
          timeoutAt: null,
          gatewayCalls: [
            { name: "CustomerDBGateway", label: "Producer cross-reference lookup", status: "Completed", stubbed: true, calledAt: ago(88), responseAt: ago(83) },
            { name: "PLUWGateway (Create Appraisal)", label: "Create appraisal record in PLUW", status: "Completed", stubbed: true, calledAt: ago(80), responseAt: ago(75) },
            { name: "UWDetermination", label: "Determine UW assignment (UA vs UST)", status: "Completed", stubbed: true, calledAt: ago(80), responseAt: ago(70) },
            { name: "PLUWGateway (Suspense)", label: "Create 14-day suspense item", status: "Completed", stubbed: true, calledAt: ago(65), responseAt: ago(60) },
            { name: "PLUWGateway (@Work)", label: "Update @Work status", status: "Completed", stubbed: true, calledAt: ago(50), responseAt: ago(45) },
            { name: "PLAPRGateway", label: "Update PLAPR staging DB", status: "Completed", stubbed: true, calledAt: ago(35), responseAt: ago(30) },
          ],
          events: [
            { timestamp: ago(90), event: "RiskIDStatusUpdateReceived", details: "StatusCode=6, InspectionType=B (UST route)", isGap: false },
            { timestamp: ago(88), event: "ProducerLookupRequested", details: "PolicyNumber=POL-67890", isGap: false },
            { timestamp: ago(83), event: "ProducerCrossReferenceRetrieved", details: "ProducerCode=PROD-002 ⚠️ MOCK DATA — Real RiskID schema TBD", isGap: true },
            { timestamp: ago(80), event: "StatusCode6UWSagaStarted", details: "Parallel: PLUW creation + UW determination", isGap: false },
            { timestamp: ago(70), event: "UWAssignmentDetermined", details: "Assignment=UST ⚠️ MOCK DATA — Real rule codes TBD", isGap: true },
            { timestamp: ago(65), event: "PLUWAppraisalCreated", details: "PLUW appraisal record created (stub)", isGap: false },
            { timestamp: ago(60), event: "SuspenseItemCreated", details: "14-day suspense (rule: InspectionType=B) ⚠️ MOCK DATA — Real suspense rules TBD", isGap: true },
            { timestamp: ago(30), event: "AppraisalUnderwriterAssigned", details: "UW=UST, all downstream systems updated", isGap: false },
          ],
        },
        {
          sagaId: "saga-ins-005",
          correlationId: "saga-ins-005",
          inspectionId: "INS-005",
          policyNumber: "POL-11111",
          statusCode: 15,
          inspectionTypeCode: "A",
          status: "Completed",
          subWorkflow: "StatusCode15Completed",
          uwAssignment: null,
          producerCode: null,
          isMockData: true,
          receivedAt: ago(45),
          completedAt: ago(5),
          timeoutAt: null,
          gatewayCalls: [
            { name: "MasterpieceGateway", label: "Transaction 90 (PLIPQP90)", status: "Completed", stubbed: true, calledAt: ago(43), responseAt: ago(38) },
            { name: "PLUWGateway (Close)", label: "Close appraisal/inspection in @Work", status: "Completed", stubbed: true, calledAt: ago(35), responseAt: ago(30) },
            { name: "PLAPRGateway", label: "Update PLAPR staging DB", status: "Completed", stubbed: true, calledAt: ago(25), responseAt: ago(20) },
          ],
          events: [
            { timestamp: ago(45), event: "RiskIDStatusUpdateReceived", details: "StatusCode=15 (Completed)", isGap: false },
            { timestamp: ago(43), event: "StatusCode15SagaStarted", details: "Masterpiece Transaction 90 path", isGap: false },
            { timestamp: ago(38), event: "MasterpieceTransactionCompleted", details: "PLIPQP90 response received ⚠️ MOCK DATA — Real Tx90 schema TBD", isGap: true },
            { timestamp: ago(30), event: "AppraisalClosedInWork", details: "@Work updated (stub)", isGap: false },
            { timestamp: ago(20), event: "PLAPRUpdated", details: "PLAPR staging DB updated (stub)", isGap: false },
            { timestamp: ago(5), event: "AppraisalCompleted", details: "Saga completed successfully", isGap: false },
          ],
        },
      ],
    });
  }
}
