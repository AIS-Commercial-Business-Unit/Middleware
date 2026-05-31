namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — saga failed (timeout or gateway error)
public sealed class AppraisalStatusUpdateFailedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset FailedAt { get; set; }
}
