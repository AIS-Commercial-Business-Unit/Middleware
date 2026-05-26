using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class ComplianceBlockedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string CheckId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset BlockedAt { get; set; } = DateTimeOffset.UtcNow;
}
