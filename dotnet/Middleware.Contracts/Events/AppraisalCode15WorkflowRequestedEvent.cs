namespace Middleware.Contracts.Events;

// UC4 · Internal events for StatusCode=15 sub-workflow coordination
public sealed class AppraisalCode15WorkflowRequestedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string InspectionTypeCode { get; set; } = string.Empty;
    public string PLUWWorkItemId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
