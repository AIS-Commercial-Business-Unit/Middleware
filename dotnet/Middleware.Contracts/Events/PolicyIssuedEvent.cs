using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class PolicyIssuedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<string> PolicyNumbers { get; set; } = [];
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? BatchId { get; set; }
    public string? RecordId { get; set; }
}
