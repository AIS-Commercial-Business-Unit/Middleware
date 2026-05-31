namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — StatusCode=6 workflow completed; UW assigned
public sealed class AppraisalUnderwriterAssignedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string UWAssignmentType { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public int SuspenseDays { get; set; }
    public string PLUWWorkItemId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
