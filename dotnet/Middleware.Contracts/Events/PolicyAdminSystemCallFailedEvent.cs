using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class PolicyAdminSystemCallFailedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;
}
