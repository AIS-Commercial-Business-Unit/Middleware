namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — PLUW appraisal/inspection created (parallel branch result)
public sealed class PLUWAppraisalCreatedEvent
{
    public string AppraisalId { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: PLUW work item ID format unknown — REPLACE_ME_PLUW_WORK_ITEM_FORMAT
    public string PLUWWorkItemId { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: @Work inspection ID format unknown — REPLACE_ME_ATWORK_INSPECTION_ID
    public string AtWorkInspectionId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
