using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class BatchItemFailedEvent : IEvent
{
    public string BatchId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string IssuanceId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
