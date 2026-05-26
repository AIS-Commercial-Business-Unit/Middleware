using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class CustomerUpdatedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<string> FieldsUpdated { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
