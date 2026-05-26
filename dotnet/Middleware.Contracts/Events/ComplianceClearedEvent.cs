using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class ComplianceClearedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string CheckId { get; set; } = string.Empty;
    public DateTimeOffset ClearedAt { get; set; } = DateTimeOffset.UtcNow;
}
