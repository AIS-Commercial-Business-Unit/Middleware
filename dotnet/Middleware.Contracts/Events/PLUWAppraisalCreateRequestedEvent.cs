namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — saga requests parallel PLUW appraisal/inspection creation
public sealed class PLUWAppraisalCreateRequestedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string ProducerCode { get; set; } = string.Empty;
    public string InspectionTypeCode { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: PLUW API request schema unknown — REPLACE_ME_PLUW_API_SCHEMA
    // Production: WCF-WSHTTP call to PLUW @Work service
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
