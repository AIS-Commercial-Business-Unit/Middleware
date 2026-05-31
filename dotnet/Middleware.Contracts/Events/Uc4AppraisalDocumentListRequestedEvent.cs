using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class Uc4AppraisalDocumentListRequestedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
