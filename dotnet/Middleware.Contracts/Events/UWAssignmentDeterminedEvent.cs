namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — UW determination result (parallel branch result)
public sealed class UWAssignmentDeterminedEvent
{
    public string AppraisalId { get; set; } = string.Empty;

    // "UA" = Underwriting Associate, "UST" = Underwriting Specialist Team
    // ⚠️ DEMO GAP: Actual UW determination rules from PRS team required — REPLACE_ME_UW_DETERMINATION_RULES
    public string UWAssignmentType { get; set; } = string.Empty;

    // Suspense period: 45 days for UA, 14 days for UST
    // ⚠️ DEMO GAP: Confirm suspense day rules with PRS — REPLACE_ME_SUSPENSE_PERIOD_RULES
    public int SuspenseDays { get; set; }

    public string AssignedTo { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset DeterminedAt { get; set; }
}
