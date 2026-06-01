using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class MainframeDocumentAggregatorTimeoutMessage : IMessage
{
    public string RequestId { get; set; } = string.Empty;
}
