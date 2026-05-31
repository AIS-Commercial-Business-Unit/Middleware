using NServiceBus;

namespace Middleware.Contracts.Commands;

// UC4 · Appraisal Documents — entry point command from Platform.Integration API
// Carries the inbound RiskID status update into the NServiceBus domain.
public sealed class ProcessAppraisalStatusUpdateCommand : ICommand
{
    public string AppraisalId { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string InspectionTypeCode { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: Real RiskID payload schema TBD — REPLACE_ME_RISKID_FULL_PAYLOAD
    public string? InspectionCompanyCode { get; set; }
    public DateTimeOffset? InspectionDate { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
}
