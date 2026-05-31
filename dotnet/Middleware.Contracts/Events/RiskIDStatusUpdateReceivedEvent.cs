namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — RiskID inbound status update
// Published by Platform.Integration when a RiskID MQ message is received (stubbed as HTTP for demo).
public sealed class RiskIDStatusUpdateReceivedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string InspectionTypeCode { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: Real RiskID MQ message has additional fields — schema unknown
    // Replace with actual RiskID payload: REPLACE_ME_RISKID_PAYLOAD_SCHEMA
    public string? InspectionCompanyCode { get; set; }
    public string? InspectorName { get; set; }
    public DateTimeOffset? InspectionDate { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
