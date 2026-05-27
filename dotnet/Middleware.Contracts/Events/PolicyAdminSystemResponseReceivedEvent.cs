using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class PolicyAdminSystemResponseReceivedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string TargetPas { get; set; } = string.Empty;
    public string AccountServiceRequestNumber { get; set; } = string.Empty;
    public List<string> PolicyNumbers { get; set; } = [];
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
