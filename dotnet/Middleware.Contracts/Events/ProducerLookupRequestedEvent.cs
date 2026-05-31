namespace Middleware.Contracts.Events;

// UC4 · Appraisal Documents — request Customer domain to resolve producer by policy number
public sealed class ProducerLookupRequestedEvent
{
    public string AppraisalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;

    // ⚠️ DEMO GAP: Exact lookup key for Customer DB is unknown — REPLACE_ME_CUSTOMER_DB_LOOKUP_KEY
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
