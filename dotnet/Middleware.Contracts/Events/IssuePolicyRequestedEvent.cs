using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class IssuePolicyRequestedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public string AccountServiceRequestNumber { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
