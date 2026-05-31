namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — Customer domain publishes producer cross-reference result
public sealed class ProducerCrossReferenceRetrievedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: ProducerCode format from real Customer DB unknown — REPLACE_ME_PRODUCER_CODE_FORMAT
    public string ProducerCode { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: UWControlCode routing rules not confirmed with PRS team — REPLACE_ME_UW_CONTROL_CODE_RULES
    // Expected values: "UA" (Underwriting Associate) or "UST" (Underwriting Specialist Team)
    public string UWControlCode { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; }
}
