using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class NotificationDispatchedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public DateTimeOffset DispatchedAt { get; set; } = DateTimeOffset.UtcNow;
}
