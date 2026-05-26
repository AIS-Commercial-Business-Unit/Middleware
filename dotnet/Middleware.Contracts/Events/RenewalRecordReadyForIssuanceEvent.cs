using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class RenewalRecordReadyForIssuanceEvent : IEvent
{
    public string BatchId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public DateTimeOffset PreparedAt { get; set; } = DateTimeOffset.UtcNow;
}
