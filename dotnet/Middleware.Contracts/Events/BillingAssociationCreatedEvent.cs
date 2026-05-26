using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class BillingAssociationCreatedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountServiceRequestNumber { get; set; } = string.Empty;
    public string BillingChannel { get; set; } = "DirectBill";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
