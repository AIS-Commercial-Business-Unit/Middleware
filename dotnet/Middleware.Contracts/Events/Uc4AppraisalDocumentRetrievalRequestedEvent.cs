using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class Uc4AppraisalDocumentRetrievalRequestedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;
}
