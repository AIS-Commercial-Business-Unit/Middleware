namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — StatusCode=15 workflow completed; appraisal closed
public sealed class AppraisalCompletedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: Masterpiece Transaction 90 response schema unknown — REPLACE_ME_MASTERPIECE_TX90_RESPONSE
    public string MasterpieceTransactionRef { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
