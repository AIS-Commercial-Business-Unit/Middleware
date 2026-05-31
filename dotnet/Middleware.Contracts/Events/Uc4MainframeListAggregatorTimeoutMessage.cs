using NServiceBus;

namespace Middleware.Contracts.Events;

public sealed class Uc4MainframeListAggregatorTimeoutMessage : IMessage
{
    public string RequestId { get; set; } = string.Empty;
}
