using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class Uc4MainframeDocumentAggregatorTimeoutMessage : IMessage
{
    public string RequestId { get; set; } = string.Empty;
}
