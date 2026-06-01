using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class AppraisalDocumentRetrievalRequestedEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;
}
