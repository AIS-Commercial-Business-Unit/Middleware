namespace Middleware.Contracts.Events;

public sealed class AppraisalSubWorkflowCompletedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string SubWorkflowType { get; set; } = string.Empty; // "StatusCode15" | "Generic"
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
