namespace Middleware.Contracts.Events;

// UC4 · Internal events for other-StatusCode sub-workflow coordination
public sealed class GenericAppraisalStatusUpdateRequestedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string PLUWWorkItemId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
