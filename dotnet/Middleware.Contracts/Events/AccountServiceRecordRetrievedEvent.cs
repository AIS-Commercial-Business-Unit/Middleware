using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class AccountServiceRecordRetrievedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountServiceRequestNumber { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;
}
