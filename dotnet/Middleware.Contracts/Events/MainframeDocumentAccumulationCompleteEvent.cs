using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class MainframeDocumentAccumulationCompleteEvent : IEvent
{
    public string RequestId { get; set; } = string.Empty;

    public string ContentBase64 { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;
}
