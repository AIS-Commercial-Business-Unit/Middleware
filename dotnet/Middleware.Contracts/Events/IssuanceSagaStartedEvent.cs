using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class IssuanceSagaStartedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
}
