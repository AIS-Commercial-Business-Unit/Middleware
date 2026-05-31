namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — saga requests parallel UW determination
public sealed class UWDeterminationRequestedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string ProducerCode { get; set; } = string.Empty;

    // InspectionTypeCode determines UA vs UST routing
    // Known values from spec: A=Appraisal, B=Appraisal UST, I=Inspection, J=Inspection
    // ⚠️ DEMO GAP: Actual rule codes not confirmed with PRS team — REPLACE_ME_UW_RULE_CODES
    public string InspectionTypeCode { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
