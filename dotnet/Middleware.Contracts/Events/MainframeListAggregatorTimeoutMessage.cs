using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class MainframeListAggregatorTimeoutMessage : IMessage
{
    public string RequestId { get; set; } = string.Empty;
}
